using System.Diagnostics.CodeAnalysis;

namespace AgtcSrvIngestion.Application.Exceptions;

[ExcludeFromCodeCoverage]
public class UnexpectedException : HttpException
{
    public UnexpectedException(Exception ex)
     : base(500, "Internal Server Error", ex) { }
}
