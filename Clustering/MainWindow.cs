using DeepLearnCS;
using Gdk;
using GLib;
using Gtk;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

public partial class MainWindow : Gtk.Window
{
	Dialog Confirm;
	FileChooserDialog TextSaver, TextLoader, ImageSaver;

	string DataFile, CentroidsFile, ClustersFile, NewDataFile, FileName;

	List<Delimiter> Delimiters = new List<Delimiter>();

	Mutex Processing = new Mutex();

	bool Paused = true;

	CultureInfo ci = new CultureInfo("en-us");

	enum Pages
	{
		DATA = 0,
		CLUSTER = 1,
		SAVE = 2,
		PLOT = 3,
		ABOUT = 4
	};

	ManagedArray InputData = new ManagedArray();
	ClusterOutput Clusters = new ClusterOutput();

	bool ClusteringInitialized;
	bool ClusteringDone;
	bool CentroidsLoaded;

	public MainWindow() : base(Gtk.WindowType.Toplevel)
	{
		Build();

		InitializeUserInterface();
	}

	protected FileFilter AddFilter(string name, params string[] patterns)
	{
		var filter = new FileFilter { Name = name };

		foreach (var pattern in patterns)
			filter.AddPattern(pattern);

		return filter;
	}

	protected void InitializeUserInterface()
	{
		Title = "GTK Clustering";

		Confirm = new Dialog(
			"Are you sure?",
			this,
			DialogFlags.Modal,
			"Yes", ResponseType.Accept,
			"No", ResponseType.Cancel
		)
		{
			Resizable = false,
			KeepAbove = true,
			TypeHint = WindowTypeHint.Dialog,
			WidthRequest = 250
		};

		Confirm.ActionArea.LayoutStyle = ButtonBoxStyle.Center;
		Confirm.WindowStateEvent += OnWindowStateEvent;

		TextSaver = new FileChooserDialog(
			"Save Text File",
			this,
			FileChooserAction.Save,
			"Cancel", ResponseType.Cancel,
			"Save", ResponseType.Accept
		);

		TextLoader = new FileChooserDialog(
			"Load Text File",
			this,
			FileChooserAction.Open,
			"Cancel", ResponseType.Cancel,
			"Load", ResponseType.Accept
		);

		TextLoader.AddFilter(AddFilter("Text files (csv/txt)", "*.txt", "*.csv"));

		TextSaver.AddFilter(AddFilter("txt", "*.txt"));
		TextSaver.AddFilter(AddFilter("csv", "*.csv"));

		ImageSaver = new FileChooserDialog(
			"Save Filtered Image",
			this,
			FileChooserAction.Save,
			"Cancel", ResponseType.Cancel,
			"Save", ResponseType.Accept
		);

		ImageSaver.AddFilter(AddFilter("png", "*.png"));
		ImageSaver.AddFilter(AddFilter("jpg", "*.jpg", "*.jpeg"));
		ImageSaver.AddFilter(AddFilter("tif", "*.tif", "*.tiff"));
		ImageSaver.AddFilter(AddFilter("bmp", "*.bmp"));
		ImageSaver.AddFilter(AddFilter("ico", "*.ico"));

		ImageSaver.Filter = ImageSaver.Filters[0];

		Delimiters.Add(new Delimiter("Tab \\t", '\t'));
		Delimiters.Add(new Delimiter("Comma ,", ','));
		Delimiters.Add(new Delimiter("Space \\s", ' '));
		Delimiters.Add(new Delimiter("Vertical Pipe |", '|'));
		Delimiters.Add(new Delimiter("Colon :", ':'));
		Delimiters.Add(new Delimiter("Semi-Colon ;", ';'));
		Delimiters.Add(new Delimiter("Forward Slash /", '/'));
		Delimiters.Add(new Delimiter("Backward Slash \\", '\\'));

		UpdateDelimiterBox(DelimiterBox, Delimiters);

		ToggleUserInterface(Paused);

		ToggleData(true);

		PlotImage.Pixbuf = Common.Pixbuf(PlotImage.WidthRequest, PlotImage.HeightRequest);

		Idle.Add(new IdleHandler(OnIdle));
	}

	protected void CopyToImage(Gtk.Image image, Pixbuf pixbuf, int OriginX, int OriginY)
	{
		if (pixbuf != null && image.Pixbuf != null)
		{
			image.Pixbuf.Fill(0);

			pixbuf.CopyArea(OriginX, OriginY, Math.Min(image.WidthRequest, pixbuf.Width), Math.Min(image.HeightRequest, pixbuf.Height), image.Pixbuf, 0, 0);

			image.QueueDraw();
		}
	}

	protected void Reset()
	{
		KMeans.Iterations = 0;

		ClearProgressBar();

		ClusteringInitialized = false;
		CentroidsLoaded = false;
		ClusteringDone = false;

		ToggleUserInterface(true);

		ToggleData(true);
	}

	protected void ToggleData(bool toggle)
	{
		MaxIterations.Sensitive = toggle;
		NumClusters.Sensitive = toggle;
		Features.Sensitive = toggle;
		DataPoints.Sensitive = toggle;
		DataView.Sensitive = toggle;
		IgnoreLastColumn.Sensitive = toggle;
		ClearDataButton.Sensitive = toggle;
	}

	protected void ToggleUserInterface(bool toggle)
	{
		CentroidsView.Sensitive = toggle;
		ClusterView.Sensitive = toggle;

		DataFileName.Sensitive = toggle;
		OpenDataButton.Sensitive = toggle;
		ReloadDataButton.Sensitive = toggle;
		DelimiterBox.Sensitive = toggle;

		ResetButton.Sensitive = toggle;
		RunButton.Sensitive = toggle;
		StopButton.Sensitive = !toggle;

		CentroidFileName.Sensitive = toggle;
		OpenCentroidsButton.Sensitive = toggle;
		SaveCentroidsButton.Sensitive = toggle;

		ClusterFileName.Sensitive = toggle;
		OpenClustersButton.Sensitive = toggle;
		SaveClustersButton.Sensitive = toggle;

		LoadCentroidsButton.Sensitive = toggle;
		SaveDataButton.Sensitive = toggle;

		NewDataFileName.Sensitive = toggle;

		PlotButton.Sensitive = toggle;
		SavePlotButton.Sensitive = toggle;
	}

	protected string GetBaseFileName(string fullpath)
	{
		return System.IO.Path.GetFileNameWithoutExtension(fullpath);
	}

	protected string GetDirectory(string fullpath)
	{
		return System.IO.Path.GetDirectoryName(fullpath);
	}

	protected void ReloadTextFile(string FileName, TextView view, bool ignoreLastColumn = false, SpinButton counter = null)
	{
		try
		{
			var categories = new List<int>();

			var current = DelimiterBox.Active;
			var delimiter = current >= 0 && current < Delimiters.Count ? Delimiters[current].Character : '\t';

			if (File.Exists(FileName) && view != null)
			{
				var text = "";
				var count = 0;

				using (TextReader reader = File.OpenText(FileName))
				{
					string line;

					while ((line = reader.ReadLine()) != null)
					{
						line = line.Trim();

						if (!string.IsNullOrEmpty(line))
						{
							var tokens = line.Split(delimiter);
							var last = tokens.Length - 1;

							if (count == 0)
							{
								Features.Value = tokens.Length > 1 ? (ignoreLastColumn ? last : tokens.Length) : tokens.Length;
							}

							if (ignoreLastColumn && tokens.Length > 1)
							{
								var cluster = SafeConvert.ToInt32(tokens[last]);

								if (!categories.Contains(cluster))
									categories.Add(cluster);
							}

							text += count > 0 ? "\n" + line : line;

							count++;
						}
					}
				}

				if (counter != null)
				{
					counter.Value = Convert.ToDouble(count, ci);
				}

				if (ignoreLastColumn)
				{
					if (categories.Count > 1)
					{
						NumClusters.Value = categories.Count;
					}
				}

				view.Buffer.Clear();

				view.Buffer.Text = text.Trim();
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: {0}", ex.Message);
		}
	}

	protected void LoadTextFile(ref string FileName, string title, TextView view, Entry entry, bool ignoreLastColumn = false, SpinButton counter = null)
	{
		TextLoader.Title = title;

		// Add most recent directory
		if (!string.IsNullOrEmpty(TextLoader.Filename))
		{
			var directory = System.IO.Path.GetDirectoryName(TextLoader.Filename);

			if (Directory.Exists(directory))
			{
				TextLoader.SetCurrentFolder(directory);
			}
		}

		if (TextLoader.Run() == (int)ResponseType.Accept)
		{
			if (!string.IsNullOrEmpty(TextLoader.Filename))
			{
				FileName = TextLoader.Filename;

				ReloadTextFile(FileName, view, ignoreLastColumn, counter);

				if (entry != null)
				{
					entry.Text = FileName;
				}
			}
		}

		TextLoader.Hide();
	}

	protected void ReloadCluster(string FileName, TextView view, SpinButton counter = null)
	{
		try
		{
			if (File.Exists(FileName) && view != null)
			{
				var text = "";
				var count = 0;

				using (TextReader reader = File.OpenText(FileName))
				{
					string line;

					while ((line = reader.ReadLine()) != null)
					{
						line = line.Trim();

						if (!string.IsNullOrEmpty(line))
						{
							var cluster = SafeConvert.ToInt32(line);

							text += count > 0 ? "\n" + cluster.ToString(ci) : cluster.ToString(ci);

							count++;
						}
					}
				}

				if (counter != null)
				{
					counter.Value = Convert.ToDouble(count, ci);
				}

				view.Buffer.Clear();

				view.Buffer.Text = text.Trim();
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: {0}", ex.Message);
		}
	}

	protected void LoadCluster(ref string FileName, string title, TextView view, Entry entry, SpinButton counter = null)
	{
		TextLoader.Title = title;

		// Add most recent directory
		if (!string.IsNullOrEmpty(TextLoader.Filename))
		{
			var directory = System.IO.Path.GetDirectoryName(TextLoader.Filename);

			if (Directory.Exists(directory))
			{
				TextLoader.SetCurrentFolder(directory);
			}
		}

		if (TextLoader.Run() == (int)ResponseType.Accept)
		{
			if (!string.IsNullOrEmpty(TextLoader.Filename))
			{
				FileName = TextLoader.Filename;

				ReloadCluster(FileName, view, counter);

				if (entry != null)
				{
					entry.Text = FileName;
				}
			}
		}

		TextLoader.Hide();
	}

	protected void SaveTextFile(ref string FileName, string title, Entry entry, ManagedArray data)
	{
		TextSaver.Title = title;

		TextSaver.SelectFilename(FileName);

		string directory;

		// Add most recent directory
		if (!string.IsNullOrEmpty(TextSaver.Filename))
		{
			directory = System.IO.Path.GetDirectoryName(TextSaver.Filename);

			if (Directory.Exists(directory))
			{
				TextSaver.SetCurrentFolder(directory);
			}
		}

		if (TextSaver.Run() == (int)ResponseType.Accept)
		{
			if (!string.IsNullOrEmpty(TextSaver.Filename))
			{
				FileName = TextSaver.Filename;

				directory = GetDirectory(FileName);

				var ext = TextSaver.Filter.Name;

				FileName = String.Format("{0}.{1}", GetBaseFileName(FileName), ext);

				if (data != null)
				{
					var current = DelimiterBox.Active;
					var delimiter = current >= 0 && current < Delimiters.Count ? Delimiters[current].Character : '\t';

					var fullpath = String.Format("{0}/{1}", directory, FileName);

					try
					{
						ManagedFile.Save2D(fullpath, data, delimiter);

						FileName = fullpath;

						entry.Text = FileName;
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error saving {0}: {1}", FileName, ex.Message);
					}
				}
			}
		}

		TextSaver.Hide();
	}

	protected void SaveTextFile(ref string FileName, string title, Entry entry, ManagedIntList data)
	{
		TextSaver.Title = title;

		TextSaver.SelectFilename(FileName);

		string directory;

		// Add most recent directory
		if (!string.IsNullOrEmpty(TextSaver.Filename))
		{
			directory = System.IO.Path.GetDirectoryName(TextSaver.Filename);

			if (Directory.Exists(directory))
			{
				TextSaver.SetCurrentFolder(directory);
			}
		}

		if (TextSaver.Run() == (int)ResponseType.Accept)
		{
			if (!string.IsNullOrEmpty(TextSaver.Filename))
			{
				FileName = TextSaver.Filename;

				directory = GetDirectory(FileName);

				var ext = TextSaver.Filter.Name;

				FileName = String.Format("{0}.{1}", GetBaseFileName(FileName), ext);

				if (data != null)
				{
					var current = DelimiterBox.Active;
					var delimiter = current >= 0 && current < Delimiters.Count ? Delimiters[current].Character : '\t';

					var fullpath = String.Format("{0}/{1}", directory, FileName);

					try
					{
						ManagedFile.Save1DY(fullpath, data);

						FileName = fullpath;

						entry.Text = FileName;
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error saving {0}: {1}", FileName, ex.Message);
					}
				}
			}
		}

		TextSaver.Hide();
	}

	protected void SaveTextFile(ref string FileName, string title, Entry entry, ManagedArray data, ManagedIntList clusters)
	{
		TextSaver.Title = title;

		TextSaver.SelectFilename(FileName);

		string directory;

		// Add most recent directory
		if (!string.IsNullOrEmpty(TextSaver.Filename))
		{
			directory = System.IO.Path.GetDirectoryName(TextSaver.Filename);

			if (Directory.Exists(directory))
			{
				TextSaver.SetCurrentFolder(directory);
			}
		}

		if (TextSaver.Run() == (int)ResponseType.Accept)
		{
			if (!string.IsNullOrEmpty(TextSaver.Filename))
			{
				FileName = TextSaver.Filename;

				directory = GetDirectory(FileName);

				var ext = TextSaver.Filter.Name;

				FileName = String.Format("{0}.{1}", GetBaseFileName(FileName), ext);

				if (data != null)
				{
					var current = DelimiterBox.Active;
					var delimiter = current >= 0 && current < Delimiters.Count ? Delimiters[current].Character : '\t';

					var fullpath = String.Format("{0}/{1}", directory, FileName);

					try
					{
						ManagedFile.Save2D(fullpath, data, clusters, delimiter);

						FileName = fullpath;

						entry.Text = FileName;
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error saving {0}: {1}", FileName, ex.Message);
					}
				}
			}
		}

		TextSaver.Hide();
	}

	protected void UpdateTextView(TextView view, ManagedArray data)
	{
		if (data != null)
		{
			var current = DelimiterBox.Active;
			var delimiter = current >= 0 && current < Delimiters.Count ? Delimiters[current].Character : '\t';

			view.Buffer.Clear();

			var text = "";

			for (int y = 0; y < data.y; y++)
			{
				if (y > 0)
					text += "\n";

				for (int x = 0; x < data.x; x++)
				{
					if (x > 0)
						text += delimiter;

					text += data[x, y].ToString(ci);
				}
			}

			view.Buffer.Text = text;
		}
	}

	protected void UpdateTextView(TextView view, ManagedIntList data)
	{
		if (data != null)
		{
			var current = DelimiterBox.Active;
			var delimiter = current >= 0 && current < Delimiters.Count ? Delimiters[current].Character : '\t';

			view.Buffer.Clear();

			var text = "";

			for (int x = 0; x < data.x; x++)
			{
				if (x > 0)
					text += "\n";

				text += data[x].ToString(ci);
			}

			view.Buffer.Text = text.Trim();
		}
	}

	protected void UpdateDelimiterBox(ComboBox combo, List<Delimiter> delimeters)
	{
		combo.Clear();

		var cell = new CellRendererText();
		combo.PackStart(cell, false);
		combo.AddAttribute(cell, "text", 0);
		var store = new ListStore(typeof(string));
		combo.Model = store;

		foreach (var delimeter in delimeters)
		{
			store.AppendValues(delimeter.Name);
		}

		combo.Active = delimeters.Count > 0 ? 0 : -1;
	}

	protected void ReparentTextView(Fixed parent, ScrolledWindow window, int x, int y)
	{
		var source = (Fixed)window.Parent;
		source.Remove(window);

		parent.Add(window);

		Fixed.FixedChild child = ((Fixed.FixedChild)(parent[window]));

		child.X = x;
		child.Y = y;
	}

	protected void ReparentLabel(Fixed parent, Label label, int x, int y)
	{
		label.Reparent(parent);

		parent.Move(label, x, y);
	}

	protected bool SetupInputData(string input)
	{
		var text = input.Trim();

		if (string.IsNullOrEmpty(text))
			return false;

		var InputBuffer = new TextBuffer(new TextTagTable())
		{
			Text = text
		};

		DataPoints.Value = Convert.ToDouble(InputBuffer.LineCount, ci);

		var inpx = Convert.ToInt32(Features.Value, ci);
		var inpy = Convert.ToInt32(DataPoints.Value, ci);

		ManagedOps.Free(InputData);

		InputData = new ManagedArray(inpx, inpy);

		var current = DelimiterBox.Active;
		var delimiter = current >= 0 && current < Delimiters.Count ? Delimiters[current].Character : '\t';
		var inputs = inpx;

		using (var reader = new StringReader(InputBuffer.Text))
		{
			for (int y = 0; y < inpy; y++)
			{
				var line = reader.ReadLine();

				if (!string.IsNullOrEmpty(line))
				{
					var tokens = line.Split(delimiter);

					if (inputs > 0 && tokens.Length >= inputs)
					{
						for (int x = 0; x < inpx; x++)
						{
							InputData[x, y] = SafeConvert.ToDouble(tokens[x]);
						}
					}
				}
			}
		}

		return true;
	}

	protected void SetupClustering()
	{
		CentroidsLoaded = false;

		var buffer = CentroidsView.Buffer.Text;

		var text = buffer.Trim();

		if (string.IsNullOrEmpty(text))
			return;

		var CentroidBuffer = new TextBuffer(new TextTagTable())
		{
			Text = text
		};

		var centroidx = Convert.ToInt32(Features.Value);
		var centroidy = Convert.ToInt32(NumClusters.Value);

		if (centroidx < 2 || centroidy < 1 || centroidy != CentroidBuffer.LineCount)
		{
			return;
		}

		var centroids = new ManagedArray(centroidx, centroidy);

		var current = DelimiterBox.Active;
		var delimiter = current >= 0 && current < Delimiters.Count ? Delimiters[current].Character : '\t';

		using (var reader = new StringReader(CentroidBuffer.Text))
		{
			for (int y = 0; y < centroidy; y++)
			{
				var line = reader.ReadLine();

				if (line != null)
				{
					var tokens = line.Split(delimiter);

					for (int x = 0; x < centroidx; x++)
					{
						if (x < tokens.Length)
						{
							centroids[x, y] = SafeConvert.ToDouble(tokens[x]);
						}
					}
				}
			}
		}

		var input = DataView.Buffer.Text.Trim();
		var iterations = Convert.ToInt32(MaxIterations.Value);

		CentroidsLoaded = SetupInputData(input) && iterations > 0;

		if (!CentroidsLoaded)
		{
			ManagedOps.Free(centroids);

			return;
		}

		KMeans.Free();
		KMeans.Setup(InputData, centroids, iterations);

		ManagedOps.Free(centroids);
	}

	protected void InitializeClustering()
	{
		ClusteringInitialized = false;

		var input = DataView.Buffer.Text.Trim();

		if (string.IsNullOrEmpty(input))
			return;

		var iterations = Convert.ToInt32(MaxIterations.Value);
		var clusters = Convert.ToInt32(NumClusters.Value);

		ClusteringInitialized = SetupInputData(input) && iterations > 0 && clusters > 0;

		KMeans.Free();

		KMeans.Setup(InputData, clusters, iterations);
	}

	protected void UpdateClusteringInfo()
	{
		if (ClusteringInitialized && ClusteringDone)
		{
			var result = KMeans.Result();

			UpdateTextView(CentroidsView, result.Centroids);
			UpdateTextView(ClusterView, result.Clusters);
		}
	}

	protected string GetFileName(string fullpath)
	{
		return System.IO.Path.GetFileNameWithoutExtension(fullpath);
	}

	protected string GetName(string fullpath)
	{
		return System.IO.Path.GetFileName(fullpath);
	}

	protected void SavePlot()
	{
		ImageSaver.Title = "Save plot";

		string directory;

		// Add most recent directory
		if (!string.IsNullOrEmpty(ImageSaver.Filename))
		{
			directory = GetDirectory(ImageSaver.Filename);

			if (Directory.Exists(directory))
			{
				ImageSaver.SetCurrentFolder(directory);
			}
		}

		if (ImageSaver.Run() == (int)ResponseType.Accept)
		{
			if (!string.IsNullOrEmpty(ImageSaver.Filename))
			{
				FileName = ImageSaver.Filename;

				directory = GetDirectory(FileName);

				var ext = ImageSaver.Filter.Name;

				var fmt = ext;

				switch (ext)
				{
					case "jpg":

						if (!FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
						{
							FileName = String.Format("{0}.jpg", GetFileName(FileName));
						}

						fmt = "jpeg";

						break;

					case "tif":

						if (!FileName.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) && !FileName.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
						{
							FileName = String.Format("{0}.tif", GetFileName(FileName));
						}

						fmt = "tiff";

						break;

					default:

						FileName = String.Format("{0}.{1}", GetFileName(FileName), ext);

						break;
				}

				if (PlotImage.Pixbuf != null)
				{
					FileName = GetName(FileName);

					var fullpath = String.Format("{0}/{1}", directory, FileName);

					try
					{
						PlotImage.Pixbuf.Save(fullpath, fmt);

						FileName = fullpath;
					}
					catch (Exception ex)
					{
						Console.WriteLine("Error saving {0}: {1}", FileName, ex.Message);
					}
				}
			}
		}

		ImageSaver.Hide();
	}

	protected void PlotClusters()
	{
		var input = DataView.Buffer.Text.Trim();

		if (string.IsNullOrEmpty(input))
			return;

		if (ClusteringDone && SetupInputData(input))
		{
			var clusters = Convert.ToInt32(NumClusters.Value, ci);

			var result = KMeans.Result();

			var pixbuf = Cluster.Plot(InputData, result.Clusters, PlotImage.WidthRequest, PlotImage.HeightRequest);

			if (pixbuf != null)
			{
				CopyToImage(PlotImage, pixbuf, 0, 0);

				Common.Free(pixbuf);
			}
		}
	}

	protected void ClearProgressBar()
	{
		ClusteringProgress.Fraction = 0.0;
		ClusteringProgress.Text = "";
	}

	protected void UpdateProgressBar()
	{
		var iterations = KMeans.Iterations;
		var maxIterations = KMeans.MaxIterations;
		var error = KMeans.Error;

		if (maxIterations > 0)
		{
			Error.Text = Convert.ToString(error, ci);

			ClusteringProgress.Fraction = Math.Round((double)iterations / maxIterations, 2);

			ClusteringProgress.Text = iterations >= maxIterations ? "Done" : String.Format("Computing ({0}%)...", Convert.ToInt32(ClusteringProgress.Fraction * 100, ci));
		}
	}

	protected bool GetConfirmation()
	{
		var confirm = Confirm.Run() == (int)ResponseType.Accept;

		Confirm.Hide();

		return confirm;
	}

	protected void CleanShutdown()
	{
		// Clean-Up Routines Here
		ManagedOps.Free(InputData);

		if (Clusters != null)
			Clusters.Free();

		Common.Free(PlotImage.Pixbuf);
		Common.Free(PlotImage);
	}

	protected void Quit()
	{
		CleanShutdown();

		Application.Quit();
	}

	protected void OnWindowStateEvent(object sender, WindowStateEventArgs args)
	{
		var state = args.Event.NewWindowState;

		if (state == WindowState.Iconified)
		{
			Confirm.Hide();
		}

		args.RetVal = true;
	}

	void OnQuitButtonClicked(object sender, EventArgs args)
	{
		OnDeleteEvent(sender, new DeleteEventArgs());
	}

	protected void OnDeleteEvent(object sender, DeleteEventArgs a)
	{
		if (GetConfirmation())
		{
			Quit();
		}

		a.RetVal = true;
	}

	bool OnIdle()
	{
		var wait = Processing.WaitOne();

		if (wait)
		{
			if (!Paused)
			{
				var result = KMeans.Step(InputData);

				if (result)
				{
					Paused = true;

					ToggleUserInterface(Paused);

					ClusteringDone = true;

					UpdateClusteringInfo();
					UpdateProgressBar();

					ClusteringInitialized = false;
					CentroidsLoaded = false;

					ToggleData(true);
				}

				var iterations = KMeans.Iterations;
				var step = Convert.ToInt32(MaxIterations.Value / 10);

				if (ClusteringInitialized && iterations % step == 0)
				{
					UpdateClusteringInfo();
					UpdateProgressBar();
				}
			}

			Processing.ReleaseMutex();
		}

		return true;
	}

	protected void OnMainNotebookSwitchPage(object sender, SwitchPageArgs args)
	{
		switch (args.PageNum)
		{
			case (int)Pages.DATA:

				ReparentLabel(PageLayoutData, LabelDataFilename, 20, 20);
				ReparentTextView(PageLayoutData, DataWindow, 20, 80);

				break;

			case (int)Pages.CLUSTER:

				ReparentLabel(PageLayoutCluster, LabelDataFilename, 20, 20);
				ReparentTextView(PageLayoutCluster, DataWindow, 20, 40);

				ReparentLabel(PageLayoutCluster, LabelClusters, 420, 20);
				ReparentTextView(PageLayoutCluster, ClusterWindow, 420, 40);

				ReparentLabel(PageLayoutCluster, LabelCentroids, 20, 190);
				ReparentTextView(PageLayoutCluster, CentroidsWindow, 20, 210);

				break;


			case (int)Pages.SAVE:

				ReparentLabel(PageLayoutSave, LabelCentroids, 20, 20);
				ReparentTextView(PageLayoutSave, CentroidsWindow, 20, 40);

				ReparentLabel(PageLayoutSave, LabelClusters, 420, 20);
				ReparentTextView(PageLayoutSave, ClusterWindow, 420, 40);

				break;

			default:

				ReparentLabel(PageLayoutData, LabelDataFilename, 20, 20);
				ReparentTextView(PageLayoutData, DataWindow, 20, 80);

				break;
		}
	}

	protected void OnAboutButtonClicked(object sender, EventArgs e)
	{
		MainNotebook.Page = (int)Pages.ABOUT;
	}

	protected void OnOpenDataButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		LoadTextFile(ref DataFile, "Load Data", DataView, DataFileName, IgnoreLastColumn.Active, DataPoints);

		Reset();
	}

	protected void OnReloadDataButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		ReloadTextFile(DataFile, DataView, IgnoreLastColumn.Active, DataPoints);

		Reset();
	}

	protected void OnClearDataButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		DataView.Buffer.Clear();

		DataPoints.Value = 0;

		Features.Value = 0;

		NumClusters.Value = 0;

		Reset();
	}

	protected void OnRunButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		Paused = false;

		ToggleUserInterface(Paused);

		if (ClusteringDone)
		{
			ClusteringDone = false;

			ClusteringInitialized = false || CentroidsLoaded;
		}

		if (!ClusteringInitialized)
		{
			InitializeClustering();
		}

		if (ClusteringInitialized)
		{
			ToggleData(false);

			UpdateProgressBar();
		}
	}

	protected void OnStopButtonClicked(object sender, EventArgs e)
	{
		if (Paused)
			return;

		Paused = true;

		ToggleUserInterface(Paused);
	}

	protected void OnResetButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		Reset();
	}

	protected void OnOpenCentroidsButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		LoadTextFile(ref CentroidsFile, "Open Centroids", CentroidsView, CentroidFileName, false, NumClusters);
	}

	protected void OnSaveCentroidsButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		if (ClusteringDone)
		{
			var result = KMeans.Result();

			SaveTextFile(ref CentroidsFile, "Save Centroids", CentroidFileName, result.Centroids);
		}
	}

	protected void OnOpenClustersButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		LoadCluster(ref ClustersFile, "Open Clusters", ClusterView, ClusterFileName, DataPoints);
	}

	protected void OnSaveClustersButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		if (ClusteringDone)
		{
			var result = KMeans.Result();

			SaveTextFile(ref ClustersFile, "Save Clusters", ClusterFileName, result.Clusters);
		}
	}

	protected void OnLoadCentroidsButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		SetupClustering();

		if (CentroidsLoaded)
		{
			ClearProgressBar();

			ClusteringDone = false;

			ToggleData(true);
		}
	}

	protected void OnSaveDataButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		if (ClusteringDone)
		{
			var result = KMeans.Result();

			SaveTextFile(ref NewDataFile, "Save New Data", NewDataFileName, InputData, result.Clusters);
		}
	}

	protected void OnPlotButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		PlotClusters();
	}

	protected void OnSavePlotButtonClicked(object sender, EventArgs e)
	{
		if (!Paused)
			return;

		SavePlot();
	}
}
