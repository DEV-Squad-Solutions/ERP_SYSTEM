using MiniErp.Application.Common.Results;

namespace MiniErp.Application.Common.Abstractions;

public interface ICurrentCompanyService
{
    Result<int> GetCompanyId();
}
