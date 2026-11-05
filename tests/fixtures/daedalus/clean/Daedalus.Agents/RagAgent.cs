using Rag.NET;

namespace Daedalus.Agents.Runtime.Retrieval;

public class RagAgent
{
    private readonly Retriever _retriever = new Retriever();

    public string Answer(string question) => _retriever.Find(question);
}
