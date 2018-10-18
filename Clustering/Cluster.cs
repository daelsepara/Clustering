using DeepLearnCS;
using Gdk;
using System;

public static class Cluster
{
	static double deltax;
	static double deltay;
	static double minx, maxx;
	static double miny, maxy;

	static int Rows(ManagedArray x)
	{
		return x.y;
	}

	static int Cols(ManagedArray x)
	{
		return x.x;
	}

	public static void Points(Pixbuf pixbuf, ManagedArray x, ManagedIntList c, Color[] colors, int f1 = 0, int f2 = 0)
	{
		f1 = f1 >= 0 && f1 < Cols(x) ? f1 : 0;
		f2 = f2 >= 0 && f2 < Cols(x) ? f2 : 0;

		if (pixbuf != null)
		{
			for (var i = 0; i < Rows(x); i++)
			{
				if (Math.Abs(deltax) > 0 && Math.Abs(deltay) > 0)
				{
					var xp = (int)((x[f1, i] - minx) / deltax);
					var yp = (int)((x[f2, i] - miny) / deltay);

					Common.Circle(pixbuf, xp, yp, 2, colors[c[i] % colors.Length]);
				}
			}
		}
	}

	public static Pixbuf Plot(ManagedArray x, ManagedIntList classification, int width, int height, int f1 = 0, int f2 = 1)
	{
		var pixbuf = Common.Pixbuf(width, height, new Color(255, 255, 255));

		var m = Rows(x);

		var xplot = new double[width];
		var yplot = new double[height];

		minx = Double.MaxValue;
		maxx = Double.MinValue;

		miny = Double.MaxValue;
		maxy = Double.MinValue;

		f1 = f1 >= 0 && f1 < Cols(x) ? f1 : 0;
		f2 = f2 >= 0 && f2 < Cols(x) ? f2 : 0;

		for (var j = 0; j < m; j++)
		{
			minx = Math.Min(x[f1, j], minx);
			maxx = Math.Max(x[f1, j], maxx);

			miny = Math.Min(x[f2, j], miny);
			maxy = Math.Max(x[f2, j], maxy);
		}

		deltax = (maxx - minx) / width;
		deltay = (maxy - miny) / height;

		minx = minx - 8 * deltax;
		maxx = maxx + 8 * deltax;
		miny = miny - 8 * deltay;
		maxy = maxy + 8 * deltay;

		deltax = (maxx - minx) / width;
		deltay = (maxy - miny) / height;

		var colors = Common.Palette2();

		colors.Shuffle();

		Points(pixbuf, x, classification, colors, f1, f2);

		// Plot bounding box
		var cw = pixbuf.Width - 1;
		var ch = pixbuf.Height;
		var border = new Color(128, 128, 128);

		Common.Line(pixbuf, 0, 1, cw, 1, border);
		Common.Line(pixbuf, cw, 1, cw, ch, border);
		Common.Line(pixbuf, 0, ch, cw, ch, border);
		Common.Line(pixbuf, 0, 1, 0, ch, border);

		return pixbuf;
	}
}
