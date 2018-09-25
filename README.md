# Clustering

The GTK-based no-programming-required K-Means clustering software for Win/Unix/Linux/OSX platforms

**About**

![About](/Screenshots/AboutPage.png)

GTK Clustering software runs K-means clustering algorithm on input data

**Data**

![Data](/Screenshots/DataPage.png)

Input data from a csv/text file can be loaded, provided you indicate the correct delimiter. Some clustering parameters are estimated based on the loaded data but you can modify it prior to running the clustering algorithm. When loading data, the last column in each line is assumed to be the cluster number. You can disable this behavior if your data sets do not contain any cluster number by un-checking the **Ignore last column** box. This will treat the last column as part of data points.

**Compute**

![Compute](/Screenshots/ComputePage.png)

This is page is where the K-means algorithm is run. The number of clusters **K** needs to be specified. The algorithm will then try to group the data points into **K** clusters. **K** points from the input data are randomly selected and used as the initial centroids. On this page, you can can also set the maximum number of iterations to run the algorithm. The sum-squared-error (**SSE**) measures the average minimum distance of the data points to the nearest cluster centroid. This is updated regularly while the algorithm is running. Running the clustering algorithm will not freeze the software program and you can freely navigate to other pages. However, some of the user interface controls may be disabled while the algorithm is running. You can start, stop, or restart the algorithm anytime.

**Cluster**

![Cluster](/Screenshots/ClusterPage.png)

On this page, you can load and save the cluster numbers and centroid values obtained by the K-means algorithm. You can also export both the input data and the assigned cluster numbers into the same text file by pressing the **Save as new data** button. You can use values inside the **Centroids** box to initialize the k-means algorithm by pressing the **Initialize centroids** button.

# Platform

GTK Clustering software has been tested on Linux, OSX, and Windows platforms.
