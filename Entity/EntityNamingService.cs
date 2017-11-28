namespace Entity
{
    using System;
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.EntityFrameworkCore.Scaffolding.Internal;

    // overrides the internal naming service used by scaffolding:
    // Microsoft.EntityFrameworkCore.Relational.Design\Internal\CandidateNamingService.cs
    public class EntityNamingService : CandidateNamingService
    {
        public override string GenerateCandidateIdentifier(string originalIdentifier)
        {
            var isValid = SyntaxFacts.IsValidIdentifier(originalIdentifier);
            if (isValid)
            {
                return originalIdentifier;
            }
            return base.GenerateCandidateIdentifier(originalIdentifier);
        }
    }
}