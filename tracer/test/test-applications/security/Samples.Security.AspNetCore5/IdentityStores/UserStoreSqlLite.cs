using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Samples.Security.AspNetCore5.Data;

namespace Samples.Security.AspNetCore5.IdentityStores;

public class UserStoreSqlLite : UserStoreBase<IdentityUser, string, IdentityUserClaim<string>, IdentityUserLogin<string>, IdentityUserToken<string>>
{
    private readonly IConfiguration _configuration;

    public UserStoreSqlLite(IConfiguration configuration)
        : base(new IdentityErrorDescriber()) =>
        _configuration = configuration;

    public override Task AddClaimsAsync(IdentityUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public override Task<IList<Claim>> GetClaimsAsync(IdentityUser user, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.FromResult<IList<Claim>>(new List<Claim>());
    }

    public override Task<IList<IdentityUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public override Task RemoveClaimsAsync(IdentityUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public override Task ReplaceClaimAsync(IdentityUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public override Task RemoveLoginAsync(IdentityUser user, string loginProvider, string providerKey, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    protected override Task RemoveUserTokenAsync(IdentityUserToken<string> token)
    {
        throw new System.NotImplementedException();
    }

    public override Task AddLoginAsync(IdentityUser user, UserLoginInfo login, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public override Task<IList<UserLoginInfo>> GetLoginsAsync(IdentityUser user, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    protected override Task AddUserTokenAsync(IdentityUserToken<string> token)
    {
        throw new System.NotImplementedException();
    }

    public override Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken cancellationToken = new CancellationToken())
    {
        var res = DatabaseHelper.ExecuteNonQuery(_configuration, $"insert into AspNetUsers(Id, Email, NormalizedEmail, EmailConfirmed, PasswordHash, TwoFactorEnabled, UserName, NormalizedUserName, PhoneNumberConfirmed, LockoutEnabled, AccessFailedCount, ConcurrencyStamp, SecurityStamp ) Values ('{Guid.NewGuid().ToString()}', '{user.Email}', '{user.NormalizedEmail}', '{(user.EmailConfirmed ? 1 : 0)}', '{user.PasswordHash}', {(user.TwoFactorEnabled ? 1 : 0)}, '{user.UserName}', '{user.NormalizedUserName}', '0', 1, 0, '{user.ConcurrencyStamp}', '{user.SecurityStamp}')");
        return Task.FromResult(res == 1 ? IdentityResult.Success : IdentityResult.Failed());
    }

    public override Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public override Task<IdentityUser> FindByIdAsync(string userId, CancellationToken cancellationToken = new CancellationToken())
    {
        var rows = DatabaseHelper.SelectDynamic(_configuration, $@"select * from AspNetUsers where Id = '{userId}'");
        var first = rows.FirstOrDefault();
        if (first is not null)
        {
            var item = (IDictionary<string, object>)first;
            return Task.FromResult(FromDicToUser(item));
        }

        return null;
    }

    public override Task<IdentityUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public override Task<IdentityUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        var rows = DatabaseHelper.SelectDynamic(_configuration, $@"select * from AspNetUsers where NormalizedUserName = '{normalizedUserName}'");

        var res = rows.FirstOrDefault();
        if (res is IDictionary<string, object> user)
        {
            return Task.FromResult(FromDicToUser(user));
        }

        return Task.FromResult<IdentityUser>(null);
    }

    private static IdentityUser FromDicToUser(IDictionary<string, object> user)
    {
        return new IdentityUser
        {
            UserName = user["UserName"]?.ToString(),
            NormalizedUserName = user["NormalizedUserName"]?.ToString(),
            Email = user["Email"]?.ToString(),
            NormalizedEmail = user["NormalizedEmail"]?.ToString(),
            PasswordHash = user["PasswordHash"]?.ToString(),
            SecurityStamp = user["SecurityStamp"]?.ToString(),
            ConcurrencyStamp = user["ConcurrencyStamp"]?.ToString(),
            Id = user["Id"]?.ToString(),
            EmailConfirmed = user["EmailConfirmed"]?.ToString() == "1"
        };
    }

    public override Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken cancellationToken = new CancellationToken())
    {
        throw new System.NotImplementedException();
    }

    public override IQueryable<IdentityUser> Users { get; }

    protected override Task<IdentityUserToken<string>> FindTokenAsync(IdentityUser user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    protected override Task<IdentityUser> FindUserAsync(string userId, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    protected override Task<IdentityUserLogin<string>> FindUserLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }

    protected override Task<IdentityUserLogin<string>> FindUserLoginAsync(string userId, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}
