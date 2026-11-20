using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CareFlow.Data;
using CareFlow.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CareFlow.Services;

public class PatientService : IPatientService
{
    private readonly CareFlowDbContext _dbContext;

    public PatientService(CareFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<PatientDto>> GetAllPatientsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Patients
            .AsNoTracking()
            .OrderBy(p => p.FullName)
            .Select(p => new PatientDto
            {
                Id = p.Id,
                FullName = p.FullName,
                DateOfBirth = p.DateOfBirth,
                ContactNumber = p.ContactNumber
            })
            .ToListAsync(cancellationToken);
    }
}
