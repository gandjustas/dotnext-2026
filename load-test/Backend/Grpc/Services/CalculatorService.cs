using Grpc.Core;

namespace Grpc.Services;

public class CalculatorService: Calcluator.CalcluatorBase
{
    public override Task<AddReply> Add(AddRequest request, ServerCallContext context)
    {
        return Task.FromResult(new AddReply
        {
            Result = request.A + request.B
        });
    }
}
