namespace Core.Domain;

public enum ConflictClass
{
    AppendOnly = 1,
    ServerValidated = 2,
    LastWriterWins = 3
}

