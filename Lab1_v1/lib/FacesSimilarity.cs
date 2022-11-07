using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;


namespace FacesSimilarity {

public class FacesComparator
{
    private static float Length(float[] v) => (float)Math.Sqrt(v.Select(x => x*x).Sum());
    public static float Distance(float[] v1, float[] v2) => Length(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());
    public static float Similarity(float[] v1, float[] v2) => v1.Zip(v2).Select(p => p.First * p.Second).Sum();
    private static DenseTensor<float> ImageToTensor(Image<Rgb24> img)
    {
        var w = img.Width;
        var h = img.Height;
        var t = new DenseTensor<float>(new[] { 1, 3, h, w });

        img.ProcessPixelRows(pa => 
        {
            for (int y = 0; y < h; y++)
            {           
                Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                for (int x = 0; x < w; x++)
                {
                    t[0, 0, y, x] = pixelSpan[x].R;
                    t[0, 1, y, x] = pixelSpan[x].G;
                    t[0, 2, y, x] = pixelSpan[x].B;
                }
            }
        });
        
        return t;
    }

    private float[] Normalize(float[] v) 
    {
        var len = Length(v);
        return v.Select(x => x / len).ToArray();
    }
    public float[] GetEmbeddings(Image<Rgb24> img) 
    {
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("data", ImageToTensor(img)) };
        sessionLock.Wait();
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
        sessionLock.Release();
        return Normalize(results.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray());
    }

    public async Task<float[]> GetEmbeddingsAsync(Image<Rgb24> img, CancellationToken ct)
    {
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("data", ImageToTensor(img)) };
        await sessionLock.WaitAsync();
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
        sessionLock.Release();
        return Normalize(results.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray());
    }

    private InferenceSession session;
    private SemaphoreSlim sessionLock;

    public FacesComparator()
    {
        using var modelStream = typeof(FacesComparator).Assembly.GetManifestResourceStream("FacesSimilarity.arcfaceresnet100-8.onnx");
        using var memoryStream = new MemoryStream();
        if (modelStream != null)
        {
            modelStream.CopyTo(memoryStream);
        }
        session = new InferenceSession(memoryStream.ToArray());
        sessionLock = new SemaphoreSlim(1, 1);
    }

    public Tuple<float, float> Compare(byte[] img1, byte[] img2)
    {
        var face1 = Image.Load<Rgb24>(img1);
        var face2 = Image.Load<Rgb24>(img2);

        var embeddings1 = GetEmbeddings(face1);
        var embeddings2 = GetEmbeddings(face2);

        return Tuple.Create(Distance(embeddings1, embeddings2), Similarity(embeddings1, embeddings2));
    }

    public async Task<Tuple<float, float>> CompareAsync(byte[] img1, byte[] img2, CancellationToken ct)
    {
        var stream1 = new MemoryStream(img1);
        var t1 = Image.LoadAsync<Rgb24>(stream1, ct);
        ct.ThrowIfCancellationRequested();

        var stream2 = new MemoryStream(img2);
        var t2 = Image.LoadAsync<Rgb24>(stream2, ct);
        ct.ThrowIfCancellationRequested();

        var face1 = await t1;
        var embeddings1 = await GetEmbeddingsAsync(face1, ct);
        ct.ThrowIfCancellationRequested();

        var face2 = await t2;
        var embeddings2 = await GetEmbeddingsAsync(face2, ct);
        ct.ThrowIfCancellationRequested();

        var distance = Distance(embeddings1, embeddings2);
        var similarity = Similarity(embeddings1, embeddings2);
        return Tuple.Create(distance, similarity);
    }
}

} // namespace FacesSimilarity