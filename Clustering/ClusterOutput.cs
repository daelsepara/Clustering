using DeepLearnCS;

public class ClusterOutput
{
	public ManagedArray Centroids;
	public ManagedIntList Clusters;

	public ClusterOutput()
	{

	}

	public ClusterOutput(ManagedArray centroids, ManagedIntList clusters)
	{
		Centroids = centroids;
		Clusters = clusters;
	}

	public void Free()
	{
		ManagedOps.Free(Centroids);
		ManagedOps.Free(Clusters);
	}
}
