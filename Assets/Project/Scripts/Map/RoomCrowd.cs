using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study3D.FpsGame.Map
{
    // RoomCrowd
    // : 한 방에 있는 몬스터끼리 겹치지 않도록 밀어내는 방향을 계산합니다. (RoomNavRegions 가 방마다 하나씩 가짐)
    // - RoomNavAgent 가 방을 찾으면 Add, 방을 떠나거나 꺼지면 Remove
    // - 매 프레임 처음 질문이 올 때 한 번, 모든 몬스터 위치를 격자 칸에 나눠 담음 (공간 해시)
    //   칸 크기 = 가장 큰 몬스터 지름 → 나와 겹칠 수 있는 이웃은 항상 내 칸 주변 3x3 칸 안에 있음
    // - 질문한 몬스터는 주변 3x3 칸만 보고, 콜라이더 반경 합보다 가까운 이웃에게서 멀어지는 방향을 더함
    //   (100마리여도 한 마리당 주변 몇 마리만 비교)
    // - 죽는 중인 몬스터(콜라이더 꺼짐)는 이웃으로 치지 않음
    public class RoomCrowd
    {
        private const int TableSize = 256;      // 해시 칸 수 (2의 거듭제곱). 다른 칸이 같은 번호에 섞여도 거리로 다시 거르므로 결과는 같음
        private const float HeightLimit = 1.5f; // 높이 차가 이보다 크면 다른 층으로 보고 밀지 않음
        private const float GoldenAngle = 2.39996f;

        private readonly List<RoomNavAgent> agents = new List<RoomNavAgent>();

        // 이번 프레임 위치 사진 (agents 와 같은 순서)
        private Vector3[] positions = new Vector3[16];
        private float[] radii = new float[16];      // 0 = 이웃으로 치지 않음
        private int[] cellOf = new int[16];

        // 칸 번호 순으로 정렬한 몬스터 번호. 칸 c 의 몬스터 = sorted[cellStart[c] .. cellStart[c + 1])
        private int[] sorted = new int[16];
        private readonly int[] cellStart = new int[TableSize + 1];
        private readonly int[] cellCursor = new int[TableSize];
        private readonly int[] visited = new int[9];

        private float cellSize = 1f;
        private int builtFrame = -1;

        public int Count => agents.Count;

        public void Add(RoomNavAgent agent)
        {
            agent.CrowdIndex = agents.Count;
            agents.Add(agent);
            builtFrame = -1;
        }

        // 마지막 몬스터를 빈자리로 옮겨서 지움
        public void Remove(RoomNavAgent agent)
        {
            int index = agent.CrowdIndex;
            if (index < 0 || index >= agents.Count || agents[index] != agent)
                return;

            int last = agents.Count - 1;
            agents[index] = agents[last];
            agents[index].CrowdIndex = index;
            agents.RemoveAt(last);
            agent.CrowdIndex = -1;
            builtFrame = -1;
        }

        // 이웃에게서 멀어지는 방향 (월드, XZ). 이웃마다 겹친 정도(0 = 닿기만 함 ~ 1 = 완전히 겹침)만큼 더함
        public Vector3 GetSeparation(RoomNavAgent agent)
        {
            if (builtFrame != Time.frameCount)
            {
                Rebuild();
            }

            int self = agent.CrowdIndex;
            Vector3 position = agent.transform.position;
            float radius = agent.Radius;
            int cellX = Cell(position.x);
            int cellZ = Cell(position.z);

            Vector3 push = Vector3.zero;
            int visitedCount = 0;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    // 주변 칸 두 개가 같은 해시 번호면 한 번만 봄
                    int hash = Hash(cellX + dx, cellZ + dz);
                    if (Array.IndexOf(visited, hash, 0, visitedCount) >= 0)
                        continue;

                    visited[visitedCount] = hash;
                    visitedCount++;

                    AddPushFromCell(hash, self, position, radius, ref push);
                }
            }
            return push;
        }

        // 해시 칸 하나에 담긴 이웃들에게서 멀어지는 방향을 push 에 더함
        private void AddPushFromCell(int hash, int self, Vector3 position, float radius, ref Vector3 push)
        {
            for (int k = cellStart[hash]; k < cellStart[hash + 1]; k++)
            {
                int other = sorted[k];
                if (other == self || radii[other] <= 0f)
                    continue;

                Vector3 offset = position - positions[other];
                if (Mathf.Abs(offset.y) > HeightLimit)
                    continue;
                offset.y = 0f;

                float minDist = radius + radii[other];
                float sq = offset.sqrMagnitude;
                if (sq >= minDist * minDist)
                    continue;

                float dist = Mathf.Sqrt(sq);
                if (dist < 1e-4f)
                {
                    // 완전히 같은 자리: 번호마다 다른 방향으로 갈라짐
                    float angle = self * GoldenAngle;
                    push += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                }
                else
                {
                    push += offset / dist * (1f - dist / minDist);
                }
            }
        }

        // 모든 몬스터 위치를 찍고 칸 번호 순으로 정렬 (칸마다 개수 세기 → 시작 위치 → 채우기)
        private void Rebuild()
        {
            builtFrame = Time.frameCount;
            int count = agents.Count;
            EnsureCapacity(count);

            TakeSnapshot(count);
            SortByCell(count);
        }

        // 위치와 반경을 찍어 두고, 가장 큰 반경으로 칸 크기를 정함
        private void TakeSnapshot(int count)
        {
            float maxRadius = 0.01f;
            for (int i = 0; i < count; i++)
            {
                RoomNavAgent agent = agents[i];
                positions[i] = agent.transform.position;
                if (agent.IsSolid)
                {
                    radii[i] = agent.Radius;
                }
                else
                {
                    radii[i] = 0f;
                }
                maxRadius = Mathf.Max(maxRadius, radii[i]);
            }
            cellSize = maxRadius * 2f;
        }

        private void SortByCell(int count)
        {
            // 칸마다 개수 세기 (cellStart[c + 1] 에 칸 c 의 개수)
            Array.Clear(cellStart, 0, cellStart.Length);
            for (int i = 0; i < count; i++)
            {
                cellOf[i] = Hash(Cell(positions[i].x), Cell(positions[i].z));
                cellStart[cellOf[i] + 1]++;
            }

            // 앞 칸들의 개수를 누적하면 칸마다 시작 위치
            for (int c = 0; c < TableSize; c++)
            {
                cellStart[c + 1] += cellStart[c];
            }

            // 칸의 시작 위치부터 차례로 채우기
            Array.Copy(cellStart, cellCursor, TableSize);
            for (int i = 0; i < count; i++)
            {
                int cell = cellOf[i];
                sorted[cellCursor[cell]] = i;
                cellCursor[cell]++;
            }
        }

        private void EnsureCapacity(int count)
        {
            if (positions.Length >= count)
                return;

            int size = Mathf.NextPowerOfTwo(count);
            positions = new Vector3[size];
            radii = new float[size];
            cellOf = new int[size];
            sorted = new int[size];
        }

        private int Cell(float v)
        {
            return Mathf.FloorToInt(v / cellSize);
        }

        // 칸 좌표 (x, z) 를 큰 소수와 섞어 0 ~ TableSize-1 번호로 (& (TableSize - 1) = TableSize 로 나눈 나머지, 2의 거듭제곱이라 가능)
        private static int Hash(int x, int z)
        {
            return ((x * 73856093) ^ (z * 19349663)) & (TableSize - 1);
        }
    }
}
