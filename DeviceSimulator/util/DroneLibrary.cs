using AddOnSimulator_SepVer.util.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;

namespace Util
{
    public static class DroneLibrary
    {
        public static DroneLibraryModel model = new DroneLibraryModel();

        public static bool LoadFromJson(string folderPath)
        {
            string jsonFilePath = Path.Combine(folderPath, "droneLibrary.json"); // 파일 이름 예시

            if (!File.Exists(jsonFilePath))
                return false;
                /*throw new FileNotFoundException($"JSON 파일이 존재하지 않습니다: {jsonFilePath}");*/

            string jsonContent = File.ReadAllText(jsonFilePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            model = JsonSerializer.Deserialize<DroneLibraryModel>(jsonContent, options);

            if(model == null || model.list.Count == 0)
                return false; // JSON 파일이 비어있거나 잘못된 형식

            CheckingInvalidCode();

            CheckingSameId();

            return true;
        }

        private static void CheckingInvalidCode()
        {
            model.list.RemoveAll(d => d.code == -1);
        }

        private static void CheckingSameId()
        {
            // 1. Dictionary에 마지막 항목을 덮어쓰기 방식으로 저장
            var lastByCode = new Dictionary<int, DroneLibraryModel.DroneInfo>();
            foreach (var item in model.list)
            {
                lastByCode[item.code] = item; // 같은 code가 있으면 마지막 항목으로 덮어씀
            }

            // 2. 마지막 항목들만 새로운 리스트로 만들기
            model.list = lastByCode.Values.ToList();
        }


        public static DroneLibraryModel.DroneInfo GetDroneByCode(int code)
        {
            return model.list.FirstOrDefault(d => d.code == code);
        }
    }
}
