using DeepLearnCS;
using System;
using System.Collections.Generic;

public static class KMeans
{
	static Random random = new Random(Guid.NewGuid().GetHashCode());
	public static int Iterations;
	public static int MaxIterations;
	public static double Error = 0.0;

	static ManagedArray Centroids = null;
	static ManagedIntList Clusters = null;
	static int NumClusters = 0;

	// Fisher–Yates shuffle algorithm
	static void Shuffle<T>(this IList<T> list)
	{
		int n = list.Count;

		for (int i = list.Count - 1; i > 1; i--)
		{
			int rnd = random.Next(i + 1);

			T value = list[rnd];

			list[rnd] = list[i];

			list[i] = value;
		}
	}

	// Initialize centroids by randomly selecting K samples from X. K is the total number of clusters.
	static ManagedArray Initialize(ManagedArray input, int clusters = 0)
	{
		var permutations = new List<int>();

		for (var i = 0; i < input.y; i++)
		{
			permutations.Add(i);
		}

		Shuffle(permutations);

		clusters = clusters == 0 ? input.y : clusters;

		var centroids = new ManagedArray(input.x, clusters);

		for (var y = 0; y < clusters; y++)
			for (var x = 0; x < input.x; x++)
				centroids[x, y] = input[x, permutations[y]];

		return centroids;
	}

	// Finds closest centroid to each example in X and assign cluster number
	static ManagedIntList Assign(ManagedArray input, ManagedArray centroids)
	{
		var m = input.y;

		var K = centroids.y;

		var distanceToCentroid = new ManagedArray(K, m);

		for (var i = 0; i < K; i++)
		{
			for (var j = 0; j < m; j++)
			{
				var sum = 0.0;

				for (var x = 0; x < input.x; x++)
				{
					var diff = input[x, j] - centroids[x, i];

					sum += diff * diff;
				}

				distanceToCentroid[i, j] = sum;
			}
		}

		var clusterList = new ManagedIntList(m, 0);

		Error = 0.0;

		for (var j = 0; j < m; j++)
		{
			var cluster = 0;

			var min = distanceToCentroid[0, j];

			for (var i = 0; i < K; i++)
			{
				if (distanceToCentroid[i, j] <= min)
				{
					min = distanceToCentroid[i, j];

					cluster = i + 1;
				}
			}

			Error += min;

			clusterList[j] = cluster;
		}

		Error /= m;

		ManagedOps.Free(distanceToCentroid);

		return clusterList;
	}

	// Computes new centroid locations
	static ManagedArray Compute(ManagedArray input, ManagedIntList clusterList, int clusters = 0)
	{
		clusters = clusters == 0 ? input.y : clusters;

		var n = input.x;

		var m = input.y;

		var centroids = new ManagedArray(n, clusters);
		var colSums = new ManagedArray(n, clusters);

		for (var cluster = 0; cluster < clusters; cluster++)
		{
			var pts = 0;

			for (var j = 0; j < m; j++)
			{
				if (clusterList[j] == cluster + 1)
				{
					pts++;

					for (var x = 0; x < n; x++)
					{
						colSums[x, cluster] += input[x, j];
					}
				}
			}

			for (var x = 0; x < n; x++)
			{
				centroids[x, cluster] = pts > 0 ? colSums[x, cluster] / pts : 0.0;
			}
		}

		ManagedOps.Free(colSums);

		return centroids;
	}

	static void CommonSetup(ManagedArray input, int iterations)
	{
		// Initialize cluster assignment
        ManagedOps.Free(Clusters);
        Clusters = new ManagedIntList(input.y, 0);

		NumClusters = Centroids.y;

		Iterations = 0;

        MaxIterations = iterations;

        Error = 0.0;
	}

	public static void Setup(ManagedArray input, int clusters, int iterations = 100)
	{
		// Initial centroids
		ManagedOps.Free(Centroids);
		Centroids = Initialize(input, clusters);

		CommonSetup(input, iterations);
	}

	public static void Setup(ManagedArray input, ManagedArray centroids, int iterations = 100)
	{
		// Initial centroids
		ManagedOps.Free(Centroids);
		Centroids = new ManagedArray(centroids);
		ManagedOps.Copy2D(Centroids, centroids, 0, 0);

		CommonSetup(input, iterations);
	}

	public static ClusterOutput Cluster(ManagedArray input, int clusters, int iterations = 100)
	{
		Setup(input, clusters, iterations);

		while (!Step(input)) { }

		return Result();
	}

	public static bool Step(ManagedArray input)
	{
		if (Iterations >= MaxIterations)
			return true;

		Iterations++;

		ManagedOps.Free(Clusters);
		Clusters = Assign(input, Centroids);

		ManagedOps.Free(Centroids);
		Centroids = Compute(input, Clusters, NumClusters);

		return Iterations >= MaxIterations;
	}

	public static ClusterOutput Result()
	{
		return new ClusterOutput(Centroids, Clusters);
	}

	public static void Free()
	{
		ManagedOps.Free(Centroids);
		ManagedOps.Free(Clusters);
	}
}
