using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Study3D.FpsGame.Data
{
    // TsvReader
    // : StreamingAssets 아래의 탭 구분 표(.tsv)를 줄마다 칸으로 잘라 돌려줍니다.
    // - 첫 줄(헤더)과 빈 줄은 건너뜁니다.
    // - 칸을 어떤 값으로 바꿀지(DTO 매핑)는 각 테이블이 정합니다.
    public static class TsvReader
    {
        // relativePath : StreamingAssets 기준 경로 (예: "Table/Cards/cards.tsv")
        public static List<string[]> ReadRows(string relativePath)
        {
            List<string[]> rows = new List<string[]>();
            string path = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (File.Exists(path) == false)
            {
                Debug.LogError($"[TsvReader] 파일이 없습니다: {path}");
                return rows;
            }

            using (StreamReader reader = new StreamReader(path))
            {
                // 첫째 줄(헤더)은 칸 이름이라 건너뜁니다.
                reader.ReadLine();
                while (reader.EndOfStream == false)
                {
                    string line = reader.ReadLine().TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    rows.Add(line.Split('\t'));
                }
            }
            return rows;
        }

        // 칸이 비어 있거나 모자라면 빈 문자열을 돌려줍니다. (뒤쪽 빈 칸이 잘려 나간 줄 대비)
        public static string GetCell(string[] cells, int index)
        {
            if (index >= cells.Length)
                return string.Empty;

            return cells[index].Trim();
        }
    }
}
