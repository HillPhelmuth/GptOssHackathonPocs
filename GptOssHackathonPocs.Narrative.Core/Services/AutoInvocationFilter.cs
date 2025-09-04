using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace GptOssHackathonPocs.Narrative.Core.Services;

public class AutoInvocationFilter : IAutoFunctionInvocationFilter
{
    public event Action<AutoFunctionInvocationContext>? OnBeforeInvocation;
    public event Action<AutoFunctionInvocationContext>? OnAfterInvocation;
    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        OnBeforeInvocation?.Invoke(context);
        Console.WriteLine($"Function {context.Function.Name} Invoking");
        await next(context);
        Console.WriteLine($"Function {context.Function.Name} Completed");
        OnAfterInvocation?.Invoke(context);
    }
}

public class FunctionInvocationFilter : IFunctionInvocationFilter
{
    public event Action<FunctionInvocationContext>? OnBeforeInvocation;
    public event Action<FunctionInvocationContext>? OnAfterInvocation;
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        OnBeforeInvocation?.Invoke(context);
        await next(context);
        OnAfterInvocation?.Invoke(context);
    }
}
