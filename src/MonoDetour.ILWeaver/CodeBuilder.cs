using System.Text;

namespace MonoDetour;

/// <summary>
/// From MonoMod.SourceGen.Internal CodeBuilder
/// </summary>
internal sealed class CodeBuilder
{
    private readonly StringBuilder sb;
    private readonly char indentChar = ' ';
    private readonly int indentAmount;

    public CodeBuilder(StringBuilder sb, int indentAmount = 2)
    {
        this.sb = sb;
        this.indentAmount = indentAmount;
    }

    private int indentLevel;
    private bool didWriteIndent;

    public CodeBuilder IncreaseIndent()
    {
        indentLevel++;
        return this;
    }

    public CodeBuilder DecreaseIndent()
    {
        indentLevel--;
        return this;
    }

    public CodeBuilder RemoveIndent()
    {
        indentLevel = 0;
        return this;
    }

    private void WriteIndentIfNeeded()
    {
        if (didWriteIndent)
        {
            return;
        }

        if (indentLevel > 0)
        {
            sb.Append(indentChar, indentLevel * indentAmount);
        }
        didWriteIndent = true;
    }

    public CodeBuilder Write(int i) =>
        Write(i.ToString());

    public CodeBuilder Write(string s)
    {
        WriteIndentIfNeeded();
        sb.Append(s);
        return this;
    }

    public CodeBuilder Write(char c)
    {
        WriteIndentIfNeeded();
        sb.Append(c);
        return this;
    }

    public CodeBuilder WriteLine(int i) =>
        WriteLine(i.ToString());

    public CodeBuilder WriteLine(string s)
    {
        WriteIndentIfNeeded();
        sb.AppendLine(s);
        didWriteIndent = false;
        return this;
    }

    public CodeBuilder WriteLine(char c)
    {
        WriteIndentIfNeeded();
        sb.Append(c).AppendLine();
        didWriteIndent = false;
        return this;
    }

    public CodeBuilder WriteLine()
    {
        sb.AppendLine();
        didWriteIndent = false;
        return this;
    }

    public override string ToString() =>
        sb.ToString();
}
