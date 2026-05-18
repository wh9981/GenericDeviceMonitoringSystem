using System;

namespace Util
{
    public class CircularQueue
    {
        private int _getpoint = 0;
        private int _setpoint = 0;
        byte[][] queue;

        public CircularQueue(int size)
        {
            queue = new byte[size][];  //커스텀 생성자 - size개의 Queue를 넣을 수 있는 생성자
        }

        //Enqueue - 새로 입력받는 데이터를 Queue에 집어넣음
        public void Enqueue(byte[] data)
        {
            queue[_setpoint] = data;
            _setpoint = (_setpoint + 1) % queue.Length;
        }


        //Queue에 존재하는 Data 중 가장 오래된 data 출력
        public byte[] Dequeue()
        {
            if (_getpoint == _setpoint)
                return new byte[0];
                
            var data = queue[_getpoint];
            _getpoint = (_getpoint + 1) % queue.Length;

            return data;
        }

        //Serial통신 시 값이 밀리기 시작하면 싹 엎고 다시 받아내기 시작
        public void QueueClear()
        {
            Array.Clear(queue, 0, queue.Length);
            _getpoint = 0;
            _setpoint = 0;
        }
    }
}
