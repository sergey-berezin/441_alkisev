using FacesSimilarity;
using System.Diagnostics;

namespace application {

class ErrorReporter: IErrorReporter
{
    public void ReportError(string msg)
    {
        Console.WriteLine($"ERROR: {msg}");
    }
}

class Program
{
    static void PrintUsage()
    {
        Console.WriteLine("USAGE:");
        Console.WriteLine("  dotnet run <image_1> <image_2>");
        Console.WriteLine("    - compare specified images");
        Console.WriteLine("  dotnet run test");
        Console.WriteLine("    - run comparison for test set of images");
        Console.WriteLine("  dotnet run asynctest");
        Console.WriteLine("    - run asynchronous comparison for test set of images");
    }

    static void PrintTable(string[] columnTitles, float[,] data)
    {
        int n = columnTitles.Length;
        
        Func<string, string> cutTitle = t => t.Length > 14 ?
            $"{t.Substring(0,3)}..{t.Substring(t.Length - 9)}" : t.Length < 8 ? new String(' ', 4) + t + new String(' ', 4) : t;
        
        Console.Write("\t\t|");
        foreach(var title in columnTitles)
        {
            Console.Write($"{cutTitle(title)}\t|");
        }
        Console.WriteLine();
        Console.WriteLine(new String('-', (n + 1) * 16 + 1));
        
        for(int i = 0; i < n; ++i)
        {
            Console.Write($" {cutTitle(columnTitles[i])}\t|");
            for(int j = 0; j < n; ++j){
                Console.Write($"   {data[i, j]:F5}\t|");
            }
            Console.WriteLine();
        }
    }
    
    static List<byte[]> ReadImages(string[] paths)
    {
        var res = new List<byte[]>();
        try
        {
            for(int i = 0; i < paths.Length; ++i)
            {
                res.Add(File.ReadAllBytes(paths[i]));
            }
        }
        catch(Exception e)
        {
            Console.WriteLine($"Failed to read files");
            Console.WriteLine(e.Message);
        }
        return res;
    }

    static void TestCore(FacesComparator fc, List<byte[]> images, float[,] distances, float[,] similarities)
    {
        var n = images.Count;
        for(int i = 0; i < n; ++i)
        {
            for(int j = 0; j < n; ++j)
            {
                (distances[i, j], similarities[i, j]) = fc.Compare(images[i], images[j]);
            }
        }
    }

    static void AsyncTestCore(FacesComparator fc, List<byte[]> images, float[,] distances, float[,] similarities)
    {
        var n = images.Count;
        var tasks = new Task<Tuple<float, float>>[n, n];
        for(int i = 0; i < n; ++i)
        {
            for(int j = 0; j < n; ++j)
            {
                tasks[i, j] = fc.CompareAsync(images[i], images[j]);
            }
        }
        for(int i = 0; i < n; ++i)
        {
            for(int j = 0; j < n; ++j)
            {
                var t = tasks[i, j].Result;
                distances[i, j] = t.Item1;
                similarities[i, j] = t.Item2;
            }
        }
    }

    static void RunTest(string[] args, bool async = false)
    {
        var n = args.Length;
        var images = ReadImages(args);
        if(images.Count != args.Length) return;

        var distances = new float[n, n];
        var similarities = new float[n, n];
        var errorReporter = new ErrorReporter();
        var fc = new FacesComparator(errorReporter);

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        if(async)
        {
            AsyncTestCore(fc, images, distances, similarities);
        }
        else{
            TestCore(fc, images, distances, similarities);
        }
        stopWatch.Stop();
        TimeSpan ts = stopWatch.Elapsed;

        Console.WriteLine("Distances:");
        PrintTable(args, distances);
        
        Console.WriteLine();
        Console.WriteLine("Similarities:");
        PrintTable(args, similarities);
        
        Console.WriteLine();
        string elapsedTime = String.Format($"{ts.Seconds}.{ts.Milliseconds}");
        Console.WriteLine("Time elapsed: " + elapsedTime);
    }

    static void Main(string[] args)
    {
        string[] testImages = {
            "images/chan1.jpg",
            "images/chan2.jpg",
            "images/depp1.jpg",
            "images/depp2.jpg",
            "images/dicaprio1.jpg",
            "images/dicaprio2.jpg"
        };

        if(args.Length == 0)
        {
            PrintUsage();
        }
        else if(args.Length == 1 && args[0] == "test"){
            RunTest(testImages);
        }
        else if(args.Length == 1 && args[0] == "asynctest")
        {
            RunTest(testImages, true);
        }
        else
        {
            RunTest(args);
        }
    }
}

} // namespace application
