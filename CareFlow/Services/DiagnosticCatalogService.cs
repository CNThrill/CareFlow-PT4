using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CareFlow.Data;
using CareFlow.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Services;

public class DiagnosticCatalogService : IDiagnosticCatalogService
{
    private readonly CareFlowDbContext _dbContext;

    public DiagnosticCatalogService(CareFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<DiagnosticServiceDto>> GetAllServicesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.DiagnosticServices
            .AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new DiagnosticServiceDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Description = s.Description,
                DailyCapacity = s.DailyCapacity
            })
            .ToListAsync(cancellationToken);
    }
}
