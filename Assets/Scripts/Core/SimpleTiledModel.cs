﻿/*
The MIT License(MIT)
Copyright(c) mxgmn 2016.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
*/

using System;
using System.Collections.Generic;
using System.Text;
using Core.Data;

namespace Core
{
	class SimpleTiledModel : Model
	{
		int[][][] propagator;

		List<SimpleTiledModelTile> tiles;
		List<string> tilenames;
		int tilesize;
		bool black;

		public SimpleTiledModel(InputSimpleTiledModelData inputData, string subsetName, int width, int height, bool periodic,
			bool black) : base(width, height)
		{
			this.periodic = periodic;
			this.black = black;

			tilesize = inputData.Size;
			bool unique = inputData.Unique;

			List<string> subset = inputData.GetSubset(subsetName);

			tiles = new List<SimpleTiledModelTile>();
			tilenames = new List<string>();
			var tempStationary = new List<double>();

			List<int[]> action = new List<int[]>();
			Dictionary<string, int> firstOccurrence = new Dictionary<string, int>();

			foreach (var tileConfig in inputData.TileConfigs)
			{
				string tilename = tileConfig.Id;
				if (subset != null && !subset.Contains(tilename)) continue;

				Func<int, int> a, b;
				int cardinality;

				SimmetryType sym = tileConfig.Symmetry;
				if (sym == SimmetryType.L)
				{
					cardinality = 4;
					a = i => (i + 1) % 4;
					b = i => i % 2 == 0 ? i + 1 : i - 1;
				}
				else if (sym == SimmetryType.T)
				{
					cardinality = 4;
					a = i => (i + 1) % 4;
					b = i => i % 2 == 0 ? i : 4 - i;
				}
				else if (sym == SimmetryType.I)
				{
					cardinality = 2;
					a = i => 1 - i;
					b = i => i;
				}
				else if (sym == SimmetryType.Slash)
				{
					cardinality = 2;
					a = i => 1 - i;
					b = i => 1 - i;
				}
				else
				{
					cardinality = 1;
					a = i => i;
					b = i => i;
				}

				T = action.Count;
				firstOccurrence.Add(tilename, T);

				int[][] map = new int[cardinality][];
				for (int t = 0; t < cardinality; t++)
				{
					map[t] = new int[8];

					map[t][0] = t;
					map[t][1] = a(t);
					map[t][2] = a(a(t));
					map[t][3] = a(a(a(t)));
					map[t][4] = b(t);
					map[t][5] = b(a(t));
					map[t][6] = b(a(a(t)));
					map[t][7] = b(a(a(a(t))));

					for (int s = 0; s < 8; s++) map[t][s] += T;

					action.Add(map[t]);
				}

				for (int t = 0; t < cardinality; t++)
				{
					tiles.Add(new SimpleTiledModelTile(tileConfig, t));
					tilenames.Add(tileConfig.Id + " " + t);
					tempStationary.Add(tileConfig.Weight);
				}
			}

			T = action.Count;
			stationary = tempStationary.ToArray();

			var tempPropagator = new bool[4][][];
			propagator = new int[4][][];
			for (int d = 0; d < 4; d++)
			{
				tempPropagator[d] = new bool[T][];
				propagator[d] = new int[T][];
				for (int t = 0; t < T; t++) tempPropagator[d][t] = new bool[T];
			}

			for (int i = 0; i < wave.Length; i++) wave[i] = new bool[T];

			foreach (NeighborData neighbor in inputData.NeighborDatas)
			{
				var leftNeighbor = neighbor.LeftNeighborId;
				var rightNeighbor = neighbor.RightNeighborId;
				if (subset != null && (!subset.Contains(leftNeighbor) || !subset.Contains(rightNeighbor))) continue;

				var leftRotation = neighbor.LeftRotation;
				int rightRotation = neighbor.RightRotation;

				int L = action[firstOccurrence[leftNeighbor]][leftRotation], D = action[L][1];
				int R = action[firstOccurrence[rightNeighbor]][rightRotation], U = action[R][1];

				tempPropagator[0][R][L] = true;
				tempPropagator[0][action[R][6]][action[L][6]] = true;
				tempPropagator[0][action[L][4]][action[R][4]] = true;
				tempPropagator[0][action[L][2]][action[R][2]] = true;

				tempPropagator[1][U][D] = true;
				tempPropagator[1][action[D][6]][action[U][6]] = true;
				tempPropagator[1][action[U][4]][action[D][4]] = true;
				tempPropagator[1][action[D][2]][action[U][2]] = true;
			}

			for (int t2 = 0; t2 < T; t2++)
			for (int t1 = 0; t1 < T; t1++)
			{
				tempPropagator[2][t2][t1] = tempPropagator[0][t1][t2];
				tempPropagator[3][t2][t1] = tempPropagator[1][t1][t2];
			}

			List<int>[][] sparsePropagator = new List<int>[4][];
			for (int d = 0; d < 4; d++)
			{
				sparsePropagator[d] = new List<int>[T];
				for (int t = 0; t < T; t++) sparsePropagator[d][t] = new List<int>();
			}

			for (int d = 0; d < 4; d++)
			for (int t1 = 0; t1 < T; t1++)
			{
				List<int> sp = sparsePropagator[d][t1];
				bool[] tp = tempPropagator[d][t1];

				for (int t2 = 0; t2 < T; t2++)
					if (tp[t2])
						sp.Add(t2);

				int ST = sp.Count;
				propagator[d][t1] = new int[ST];
				for (int st = 0; st < ST; st++) propagator[d][t1][st] = sp[st];
			}
		}

		protected override void Propagate()
		{
			while (stacksize > 0)
			{
				int i1 = stack[stacksize - 1];
				changes[i1] = false;
				stacksize--;

				bool[] w1 = wave[i1];
				int x1 = i1 % FMX, y1 = i1 / FMX;

				for (int d = 0; d < 4; d++)
				{
					int x2 = x1, y2 = y1;
					if (d == 0)
					{
						if (x1 == FMX - 1)
						{
							if (!periodic) continue;
							else x2 = 0;
						}
						else x2 = x1 + 1;
					}
					else if (d == 1)
					{
						if (y1 == 0)
						{
							if (!periodic) continue;
							else y2 = FMY - 1;
						}
						else y2 = y1 - 1;
					}
					else if (d == 2)
					{
						if (x1 == 0)
						{
							if (!periodic) continue;
							else x2 = FMX - 1;
						}
						else x2 = x1 - 1;
					}
					else
					{
						if (y1 == FMY - 1)
						{
							if (!periodic) continue;
							else y2 = 0;
						}
						else y2 = y1 + 1;
					}

					int i2 = x2 + y2 * FMX;
					bool[] w2 = wave[i2];
					int[][] prop = propagator[d];

					for (int t2 = 0; t2 < T; t2++)
						if (w2[t2])
						{
							bool b = false;
							int[] p = prop[t2];
							for (int l = 0; l < p.Length && !b; l++) b = w1[p[l]];

							if (!b)
							{
								Change(i2);
								w2[t2] = false;
							}
						}
				}
			}
		}

		protected override bool OnBoundary(int i)
		{
			return false;
		}

		/*
		public override Bitmap Graphics()
		{
			Bitmap result = new Bitmap(FMX * tilesize, FMY * tilesize);
			int[] bitmapData = new int[result.Height * result.Width];
	
			if (observed != null)
			{
				for (int x = 0; x < FMX; x++) for (int y = 0; y < FMY; y++)
						{
							Color[] tile = tiles[observed[x + y * FMX]];
							for (int yt = 0; yt < tilesize; yt++) for (int xt = 0; xt < tilesize; xt++)
								{
									Color c = tile[xt + yt * tilesize];
									bitmapData[x * tilesize + xt + (y * tilesize + yt) * FMX * tilesize] =
										unchecked((int)0xff000000 | (c.R << 16) | (c.G << 8) | c.B);
								}
						}
			}
			else
			{
				for (int x = 0; x < FMX; x++) for (int y = 0; y < FMY; y++)
					{
						bool[] a = wave[x + y * FMX];
						int amount = (from b in a where b select 1).Sum();
						double lambda = 1.0 / (from t in Enumerable.Range(0, T) where a[t] select stationary[t]).Sum();
	
						for (int yt = 0; yt < tilesize; yt++) for (int xt = 0; xt < tilesize; xt++)
							{
								if (black && amount == T) bitmapData[x * tilesize + xt + (y * tilesize + yt) * FMX * tilesize] = unchecked((int)0xff000000);
								else
								{
									double r = 0, g = 0, b = 0;
									for (int t = 0; t < T; t++) if (wave[x + y * FMX][t])
										{
											Color c = tiles[t][xt + yt * tilesize];
											r += (double)c.R * stationary[t] * lambda;
											g += (double)c.G * stationary[t] * lambda;
											b += (double)c.B * stationary[t] * lambda;
										}
	
									bitmapData[x * tilesize + xt + (y * tilesize + yt) * FMX * tilesize] =
										unchecked((int)0xff000000 | ((int)r << 16) | ((int)g << 8) | (int)b);
								}
							}
					}
			}
	
			var bits = result.LockBits(new Rectangle(0, 0, result.Width, result.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			System.Runtime.InteropServices.Marshal.Copy(bitmapData, 0, bits.Scan0, bitmapData.Length);
			result.UnlockBits(bits);
	
			return result;
		}
		*/

		public string TextOutput()
		{
			var result = new StringBuilder();

			for (int y = 0; y < FMY; y++)
			{
				for (int x = 0; x < FMX; x++) result.Append(tilenames[observed[x + y * FMX]] + ", ");
				result.Append(Environment.NewLine);
			}

			return result.ToString();
		}
	}
}
