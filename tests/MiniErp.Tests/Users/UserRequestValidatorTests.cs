using FluentValidation;
using MiniErp.Application.Features.Users;

namespace MiniErp.Tests.Users;

public sealed class UserRequestValidatorTests
{
    [Fact]
    public async Task CreateValidator_RejectsNullCompanyIds()
    {
        var validator = new UserCreateRequestValidator();
        var request = new UserCreateRequest(
            "user",
            "user@example.com",
            "First",
            "Last",
            null,
            "P@ssword123",
            ["User"],
            null!);

        var result = await validator.ValidateAsync(request);

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(UserCreateRequest.CompanyIds));
    }

    [Fact]
    public async Task UpdateValidator_RejectsNullCompanyIds()
    {
        var validator = new UserUpdateRequestValidator();
        var request = new UserUpdateRequest(
            "user",
            "user@example.com",
            "First",
            "Last",
            null,
            ["User"],
            null!);

        var result = await validator.ValidateAsync(request);

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(UserUpdateRequest.CompanyIds));
    }

    [Fact]
    public async Task CompaniesValidator_RejectsNullCompanyIds()
    {
        var validator = new UserCompaniesRequestValidator();
        var request = new UserCompaniesRequest(null!);

        var result = await validator.ValidateAsync(request);

        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                nameof(UserCompaniesRequest.CompanyIds));
    }
}
