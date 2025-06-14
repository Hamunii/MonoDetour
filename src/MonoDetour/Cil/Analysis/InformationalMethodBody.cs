using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using static MonoDetour.Cil.Analysis.IInformationalInstruction;
using static MonoDetour.Cil.Analysis.InformationalInstruction;

namespace MonoDetour.Cil.Analysis;

/// <summary>
/// A Mono.Cecil <see cref="MethodBody"/> wrapper
/// which contains a list of <see cref="IInformationalInstruction"/>
/// in place of a Mono.Cecil <see cref="Instruction"/> list for easier analysis.
/// </summary>
public interface IInformationalMethodBody
{
    /// <summary>
    /// The original <see cref="MethodBody"/>.
    /// </summary>
    MethodBody Body { get; }

    /// <summary>
    /// The list of <see cref="IInformationalInstruction"/>s in the
    /// <see cref="IInformationalMethodBody"/>.
    /// </summary>
    ReadOnlyCollection<IInformationalInstruction> InformationalInstructions { get; }

    /// <summary>
    /// Gets an <see cref="IInformationalInstruction"/> for the specified <see cref="Instruction"/>.
    /// </summary>
    /// <param name="instruction">The <see cref="Instruction"/> whose
    /// <see cref="IInformationalInstruction"/> to get.</param>
    /// <returns>An <see cref="IInformationalInstruction"/> for the instruction.</returns>
    public IInformationalInstruction GetInformationalInstruction(Instruction instruction);

    /// <summary>
    /// Checks if <see cref="InformationalInstructions"/> contains error annotations.
    /// </summary>
    /// <returns>True if error annotations, otherwise false.</returns>
    bool HasErrors();

    /// <summary>
    /// Returns a string presentation of this <see cref="IInformationalMethodBody"/>.
    /// </summary>
    string ToString();

    /// <summary>
    /// Returns a string presentation of this <see cref="IInformationalMethodBody"/>,
    /// including error annotations.
    /// </summary>
    string ToStringWithAnnotations();

    /// <summary>
    /// Returns a string presentation of this <see cref="IInformationalMethodBody"/>
    /// with only instructions with error annotations.
    /// </summary>
    string ToStringWithAnnotationsExclusive();
}

internal sealed class InformationalMethodBody : IInformationalMethodBody
{
    public MethodBody Body { get; }
    public ReadOnlyCollection<IInformationalInstruction> InformationalInstructions { get; }
    public HashSet<Instruction> Duplicates { get; } = [];
    public bool HasDuplicates => Duplicates.Count != 0;
    private readonly Dictionary<Instruction, InformationalInstruction> map = [];

    private InformationalMethodBody(MethodBody body)
    {
        Body = body;
        List<IInformationalInstruction> informationalInstructions = [];

        var bodyInstructions = body.Instructions;
        if (bodyInstructions.Count == 0)
        {
            InformationalInstructions = informationalInstructions.AsReadOnly();
            return;
        }

        HashSet<Instruction> originalInstructions = [];

        foreach (var instruction in bodyInstructions)
        {
            if (!originalInstructions.Add(instruction))
            {
                Duplicates.Add(instruction);
            }
        }

        body.Method.RecalculateILOffsets();

        InformationalInstruction first = null!;
        InformationalInstruction? previous = null;

        for (int i = 0; i < bodyInstructions.Count; i++)
        {
            var cecilIns = bodyInstructions[i];

            List<IHandlerInfo> handlerParts = [];

            foreach (var eh in body.ExceptionHandlers)
            {
                HandlerPart handlerPart = 0;

                if (eh.TryStart.Previous == cecilIns)
                    handlerPart |= HandlerPart.BeforeTryStart;

                if (eh.TryStart == cecilIns)
                    handlerPart |= HandlerPart.TryStart;
                if (eh.TryEnd == cecilIns)
                    handlerPart |= HandlerPart.TryEnd;
                if (eh.FilterStart == cecilIns)
                    handlerPart |= HandlerPart.FilterStart;
                if (eh.HandlerStart == cecilIns)
                    handlerPart |= HandlerPart.HandlerStart;
                if (eh.HandlerEnd == cecilIns)
                    handlerPart |= HandlerPart.HandlerEnd;

                if (handlerPart == 0)
                    continue;

                handlerParts.Add(new HandlerInfo(handlerPart, eh));
            }

            InformationalInstruction ins = new(cecilIns, default, default, handlerParts);
            informationalInstructions.Add(ins);
            map[cecilIns] = ins;

            if (Duplicates.Contains(cecilIns))
            {
                ins.ErrorAnnotations.Add(new AnnotationDuplicateInstance());
            }

            if (previous is null)
            {
                first = ins;
            }
            else
            {
                previous.next = ins;
            }

            ins.Previous = previous;
            previous = ins;
        }

        int stackSize = 0;
        CrawlInstructions(first, map, stackSize, body, distance: 0);

        foreach (var eh in body.ExceptionHandlers)
        {
            if (!map.TryGetValue(eh.TryStart, out var tryStart))
            {
                continue;
            }

            if (!tryStart.IsReachable)
            {
                continue;
            }

            CrawlExceptionHandler(eh);
        }

        InformationalInstructions = informationalInstructions.AsReadOnly();
    }

    public void CrawlExceptionHandler(ExceptionHandler handler)
    {
        int stackSize = 0;

        if (handler.HandlerType is ExceptionHandlerType.Filter or ExceptionHandlerType.Catch)
        {
            stackSize = 1;
        }

        if (handler.HandlerStart is null || handler.HandlerEnd is null)
        {
            return;
        }

        var handlerStart = handler.HandlerStart;
        var handlerEnd = handler.HandlerEnd;

        CrawlInstructions(
            map[handlerStart],
            map,
            stackSize,
            Body,
            map[handlerEnd].RelativeDistance - 9_000,
            outsideExceptionHandler: false
        );

        if (handler.FilterStart is null)
        {
            return;
        }

        var filterStart = handler.FilterStart;

        CrawlInstructions(
            map[filterStart],
            map,
            stackSize,
            Body,
            map[handlerEnd].RelativeDistance - 10_000 + 1,
            outsideExceptionHandler: false
        );
    }

    public static InformationalMethodBody CreateInformationalSnapshotJIT(MethodBody body) =>
        new(body);

    public static InformationalMethodBody CreateInformationalSnapshotEvaluateAll(MethodBody body)
    {
        InformationalMethodBody informationalBody = new(body);

        foreach (var eh in body.ExceptionHandlers)
        {
            if (eh.HandlerEnd is null)
            {
                continue;
            }

            informationalBody.map[eh.HandlerEnd].RelativeDistance = int.MinValue + 100_000;
            informationalBody.CrawlExceptionHandler(eh);
        }

        foreach (var ins in informationalBody.InformationalInstructions.Where(x => !x.IsReachable))
        {
            if (ins is not InformationalInstruction inf)
            {
                // This should be unreachable.
                throw new InvalidCastException("Not InformationalInstruction!");
            }

            // Make IncomingStackSize produce a reasonable stack size
            inf.PreviousChronological = inf.Previous;

            int startStackSize = inf.Previous!.StackSize;

            // A negative stack size is almost definitely wrong which would happen
            // if there is something like two ret instructions in a row for example.
            if (startStackSize < 0)
            {
                startStackSize = 0;
            }

            CrawlInstructions(
                inf,
                informationalBody.map,
                stackSize: startStackSize,
                body,
                int.MinValue
            );
        }

        return informationalBody;
    }

    public IInformationalInstruction GetInformationalInstruction(Instruction instruction)
    {
        if (map.TryGetValue(instruction, out var informationalInstruction))
        {
            return informationalInstruction;
        }

        throw new KeyNotFoundException(
            $"The instruction '{instruction}' is not a part of the {nameof(IInformationalMethodBody)}."
        );
    }

    public override string ToString() => Body.Method.FullName + "\n" + ToStringWithAnnotations();

    /// <summary>
    /// To string with annotations.
    /// </summary>
    public string ToStringWithAnnotations()
    {
        StringBuilder sb = new();

        foreach (var instruction in InformationalInstructions)
        {
            sb.AppendLine(instruction.ToStringWithAnnotations());
        }

        return sb.ToString();
    }

    /// <summary>
    /// To string with annotations. Excludes all other instructions.
    /// </summary>
    public string ToStringWithAnnotationsExclusive()
    {
        StringBuilder sb = new();

        foreach (var instruction in InformationalInstructions.Where(x => x.HasErrorAnnotations))
        {
            sb.AppendLine(instruction.ToStringWithAnnotations());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if the list of informational instructions has annotated errors.
    /// </summary>
    /// <returns>Whether or not the list of informational instructions has annotated errors.</returns>
    public bool HasErrors()
    {
        // Technically duplicate instructions aren't errors, but they can cause them.
        if (Duplicates.Count != 0 || InformationalInstructions.Any(x => x.HasErrorAnnotations))
        {
            return true;
        }

        return false;
    }

    internal void ThrowIfErrorAnnotations()
    {
        if (HasErrors())
        {
            throw new Exception("Informational instructions had exception annotations.");
        }
    }

    internal void ThrowIfNoErrorAnnotations()
    {
        if (!HasErrors())
        {
            throw new Exception("Informational instructions had exception annotations.");
        }
    }
}

internal static class IInformationalMethodBodyExtensions
{
    internal static string ToErrorMessageString(this IInformationalMethodBody informationalBody)
    {
        StringBuilder sb = new();
        sb.AppendLine("An ILHook manipulation target method threw on compilation:");
        sb.AppendLine(informationalBody.Body.Method.FullName);
        sb.AppendLine("--- MonoDetour CIL Analysis Start Full Method ---");
        sb.AppendLine();
        sb.AppendLine("Info: Stack size is on the left, instructions are on the right.");
        sb.AppendLine();

        if (informationalBody.InformationalInstructions.Count == 0)
        {
            sb.AppendLine("Method has 0 instructions.");
            goto analysisEnd;
        }

        sb.Append(informationalBody.ToStringWithAnnotations());

        sb.AppendLine();
        sb.AppendLine("--- MonoDetour CIL Analysis Summary ---");
        sb.AppendLine();

        if (!informationalBody.HasErrors())
        {
            sb.AppendLine("MonoDetour didn't catch any mistakes.")
                .AppendLine("The errors may not be directly stack behavior related.")
                .AppendLine("Operands or types on the stack aren't validated.")
                .AppendLine("You can improve the analysis:")
                .AppendLine(
                    "https://github.com/MonoDetour/MonoDetour/blob/main/src/MonoDetour/Cil/Analysis/CilAnalyzer.cs"
                );
        }
        else
        {
            sb.AppendLine("Info: Stack size is on the left, instructions are on the right.");
            sb.AppendLine();

            sb.Append(informationalBody.ToStringWithAnnotationsExclusive());

            sb.AppendLine();
            sb.AppendLine("Note: This analysis may not be perfect.");
        }

        analysisEnd:
        sb.AppendLine();
        sb.Append("--- MonoDetour CIL Analysis End ---");

        return sb.ToString();
    }
}
