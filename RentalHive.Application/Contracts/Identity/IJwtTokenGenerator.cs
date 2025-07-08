using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RentalHive.Domain.Entities;

namespace RentalHive.Application.Contracts.Identity
{
    /// <summary>
    /// Defines the contract for a JWT token generation service.
    /// </summary>
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user);
    }
}
