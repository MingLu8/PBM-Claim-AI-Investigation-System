//using Microsoft.Extensions.AI;
//using Microsoft.SemanticKernel.Embeddings;
//namespace ApiGateway.Extensions;
//#pragma warning disable SKEXP0001
//public class MeaiToSkEmbeddingAdapter : ITextEmbeddingGenerationService
//{
//    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

//    public MeaiToSkEmbeddingAdapter(IEmbeddingGenerator<string, Embedding<float>> generator)
//    {
//        _generator = generator;
//    }

//    public IReadOnlyDictionary<string, object?> Attributes => throw new NotImplementedException();

//    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> data, Kernel? kernel = null, CancellationToken cancellationToken = default)
//    {
//        // Call the modern generator
//        var result = await _generator.GenerateAsync(data, cancellationToken: cancellationToken);
//        // Map the new Embedding<float> objects to the legacy ReadOnlyMemory<float> list
//        return result.Select(e => e.Vector).ToList();
//    }
//}
//#pragma warning restore SKEXP0001
