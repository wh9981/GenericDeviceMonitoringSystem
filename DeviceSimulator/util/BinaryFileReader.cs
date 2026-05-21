using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace Util
{
    public class BinaryFileReader
    {
        /// <summary>
        /// 특정 경로의 .bin 파일을 읽어 송신 메서드에 전달하는 클래스
        /// </summary>

        private CancellationTokenSource _lts;    // 외부 CTS를 기반으로 LinkedTokenSource 생성

        private string FolderPath { get; set; }


        public Action<string> DataSendEvent;

        public BinaryFileReader(string _folderPath)
        {
            FolderPath = _folderPath;
        }

        /// <summary>
        /// bin파일 읽어올 폴더 경로 변경
        /// </summary>
        public void ChangePath(string newPath)
        {
            FolderPath = newPath;
        }

        /// <summary>
        /// bin파일을 읽어 송신 메서드에 전달하는 작업을 시작. 기존 작업이 있다면 취소 후 새로 시작.
        /// </summary>
        public void StartRead(Func<byte[], CancellationToken, Task<bool>> sendMethods, CancellationToken token)
        {
            StopRead(); // 기존 작업이 있다면 취소

            var lts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _lts = lts; // LinkedTokenSource를 클래스 멤버로 저장

            Task.Run(async () => await ReadFile(sendMethods, lts));
        }

        /// <summary>
        /// 현재 진행 중인 파일 읽기 작업을 취소. 이후 StartRead 호출 시 새 작업이 시작됨.
        /// </summary>
        public void StopRead()
        {
            if (_lts != null && !_lts.IsCancellationRequested)
            {
                _lts.Cancel();
            }
        }

        /// <summary>
        /// 파일 경로를 받아 해당 파일을 바이너리 형태로 읽어 반환하는 메서드
        /// </summary>
        private byte[] ReadBinaryFile(string filePath)
        {
            // 파일을 바이너리 형태로 읽기
            byte[] fileContent = File.ReadAllBytes(filePath);
            return fileContent;
        }

        /// <summary>
        /// 폴더 내의 모든 .bin 파일을 읽어 송신 메서드에 전달하는 작업을 수행. 취소 요청이 있을 때까지 반복적으로 수행.
        /// </summary>
        private async Task ReadFile(Func<byte[], CancellationToken, Task<bool>> sendMethods, CancellationTokenSource lts)
        {
            var token = lts.Token;

            // 해당 폴더의 모든 .bin 파일을 읽어옴
            string[] fileEntries = Directory.GetFiles(FolderPath, "*.bin");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (string fileName in fileEntries)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        // 각 파일을 처리 (예: 파일 내용 읽기)
                        var packets = ReadBinaryFile(fileName);

                        try
                        {
                            bool result = await sendMethods(packets, token);
                            if (!result)
                                break;
                        }
                        catch (Exception ex)
                        {
                            if (ex is OperationCanceledException)
                            {
                                DataSendEvent?.Invoke("Stop Read File Task");
                                return;
                            }

                            DataSendEvent?.Invoke($"{ex.Message} in ReadFile");
                            return;
                        }
                        // Console.WriteLine(fileName + "읽기 완료");
                    }
                }
            }
            finally
            {
                lts.Dispose();

                if (ReferenceEquals(_lts, lts))
                    _lts = null;
            }
        }
    }
}