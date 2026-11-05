using Rag.NET;

namespace Daedalus.Infrastructure.Indexing;

public class RagIndexer
{
    private readonly Retriever _retriever = new Retriever();

    public void Index(string document) => _retriever.Find(document);
}
