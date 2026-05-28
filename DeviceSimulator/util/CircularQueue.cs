using System;

namespace Util
{
    public class CircularQueue
    {
        private readonly object _obj = new object();
        private int Getpoint { get; set; } = 0;
        private int Setpoint { get; set; } = 0;
        private int Count { get; set; } = 0;
        byte[] queue;

        public CircularQueue(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Queue size must be greater than 0.");

            queue = new byte[size];  //커스텀 생성자 - size개의 Queue를 넣을 수 있는 생성자
        }

        //Enqueue - 새로 입력받는 데이터를 Queue에 집어넣음
        public void Enqueue(byte data)
        {
            lock (_obj)
            {
                queue[Setpoint] = data;
                Setpoint = (Setpoint + 1) % queue.Length;

                // 버퍼가 가득 찬 상태에서는 가장 오래된 데이터를 버리고 최신 데이터를 유지한다.
                if (Count == queue.Length)
                    Getpoint = (Getpoint + 1) % queue.Length;
                else
                    Count++;
            }
        }

        public void Enqueue(byte[] data, int enqueueSize = 0)
        {
            lock (_obj)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                var length = enqueueSize == 0 ? data.Length : enqueueSize;
                if (length < 0 || length > data.Length)
                    throw new ArgumentOutOfRangeException(nameof(enqueueSize));

                if (length == 0)
                    return;

                // 입력 데이터가 버퍼보다 크면 최신 구간만 보존한다.
                if (length >= queue.Length)
                {
                    Buffer.BlockCopy(data, length - queue.Length, queue, 0, queue.Length);
                    Getpoint = 0;
                    Setpoint = 0;
                    Count = queue.Length;
                    return;
                }

                // 새 데이터가 남은 용량보다 크면 초과분만큼 읽기 위치를 밀어 오래된 데이터를 폐기한다.
                var overflow = Count + length - queue.Length;
                if (overflow > 0)
                {
                    Getpoint = (Getpoint + overflow) % queue.Length;
                    Count -= overflow;
                }

                // Setpoint가 배열 끝에 걸치면 뒤쪽과 앞쪽으로 나누어 복사한다.
                int remainingSpace = queue.Length - Setpoint;
                if (length > remainingSpace)
                {
                    //data 크기가 남은 공간보다 클때 앞뒤로 분할하기.
                    Buffer.BlockCopy(data, 0, queue, Setpoint, remainingSpace); Setpoint = 0;
                    Buffer.BlockCopy(data, remainingSpace, queue, Setpoint, length - remainingSpace);
                    Setpoint = length - remainingSpace;
                }
                else if (length == remainingSpace)
                {
                    Buffer.BlockCopy(data, 0, queue, Setpoint, length);
                    Setpoint = 0;
                }
                else
                {
                    Buffer.BlockCopy(data, 0, queue, Setpoint, length);
                    Setpoint += length;
                }

                Count += length;
            }
        }

        //Queue에 존재하는 Data 중 가장 오래된 data 출력
        public byte? Dequeue()
        {
            lock (_obj)
            {
                if (Count == 0)
                    return null;

                byte data = queue[Getpoint];
                Getpoint = (Getpoint + 1) % queue.Length;
                Count--;

                return data;
            }
        }

        // 반환값: 배열 대신 '실제로 꺼낸 바이트 수'를 int로 반환!
        public int Dequeue(byte[] destinationBuffer, int getCount)
        {
            lock (_obj) // 자물쇠 완벽!
            {
                if (destinationBuffer == null)
                    throw new ArgumentNullException(nameof(destinationBuffer));

                if (getCount <= 0 || destinationBuffer.Length == 0)
                    return 0;

                // 1. 현재 큐에 남아있는 실제 데이터 개수
                int available = Count;

                // 큐가 비어있으면 0 반환 (꺼낼 게 없음)
                if (available == 0) return 0;

                // 2. 요청한 개수와 남은 개수 중 작은 값을 선택 (핵심 방어 로직!)
                int actualCount = Math.Min(Math.Min(getCount, destinationBuffer.Length), available);

                // Getpoint가 배열 끝에 걸치면 뒤쪽과 앞쪽에서 나누어 꺼낸다.
                // 3. 실제 줄 수 있는 양(actualCount)만큼만 복사
                if (Getpoint + actualCount >= queue.Length)
                {
                    var lesCnt = queue.Length - Getpoint;
                    Buffer.BlockCopy(queue, Getpoint, destinationBuffer, 0, lesCnt);
                    Buffer.BlockCopy(queue, 0, destinationBuffer, lesCnt, actualCount - lesCnt);
                    Getpoint = actualCount - lesCnt;
                }
                else
                {
                    Buffer.BlockCopy(queue, Getpoint, destinationBuffer, 0, actualCount);
                    Getpoint += actualCount;
                }

                Count -= actualCount;

                // 4. "나 이만큼 꺼내서 그릇에 담았어!" 하고 밖으로 알려줌
                return actualCount;
            }
        }

        //Serial통신 시 값이 밀리기 시작하면 싹 엎고 다시 받아내기 시작
        public void QueueClear()
        {
            lock (_obj)
            {
                Array.Clear(queue, 0, queue.Length);
                Getpoint = 0;
                Setpoint = 0;
                Count = 0;
            }
        }
    }
}
