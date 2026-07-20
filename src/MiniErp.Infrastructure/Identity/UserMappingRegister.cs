using Mapster;
using MiniErp.Application.Features.Users;

namespace MiniErp.Infrastructure.Identity;

public sealed class UserMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<UserCreateRequest, ApplicationUser>()
            .Map(user => user.UserName, request => request.UserName.Trim())
            .Map(user => user.Email, request => request.Email.Trim())
            .Map(user => user.FirstName, request => request.FirstName.Trim())
            .Map(user => user.LastName, request => request.LastName.Trim())
            .Map(
                user => user.PhoneNumber,
                request => string.IsNullOrWhiteSpace(request.PhoneNumber)
                    ? null
                    : request.PhoneNumber.Trim())
            .Map(user => user.ProfileImage, _ => string.Empty)
            .Map(user => user.EmailConfirmed, _ => true);

        config.NewConfig<UserUpdateRequest, ApplicationUser>()
            .Map(user => user.UserName, request => request.UserName.Trim())
            .Map(user => user.Email, request => request.Email.Trim())
            .Map(user => user.FirstName, request => request.FirstName.Trim())
            .Map(user => user.LastName, request => request.LastName.Trim())
            .Map(
                user => user.PhoneNumber,
                request => string.IsNullOrWhiteSpace(request.PhoneNumber)
                    ? null
                    : request.PhoneNumber.Trim());
    }
}
