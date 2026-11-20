using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CareFlow.DTOs;

namespace CareFlow.Services;

public interface IDiagnosticCatalogService
{
    Task<List<DiagnosticServiceDto>> GetAllServicesAsync(CancellationToken cancellationToken = default);
}
