using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rava.Core.Dtos;
using Rava.Infrastructure.Services;

namespace Rava.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/player")]
[RequestSizeLimit(ProfileAvatarUploadLimits.MaxBytes)]
public class PlayerController(
    PlayerGameService gameService,
    CompanyNameService companyNameService,
    PlayerMessageService playerMessageService,
    PeerMessageService peerMessageService,
    PlayerToStaffMessageService playerToStaffMessageService) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<PlayerProfileResponse>> GetMyProfile(CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        var profile = await gameService.GetProfileAsync(playerId, playerId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("profile/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PlayerProfileResponse>> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose an image file to upload." });
        }

        await using var stream = file.OpenReadStream();
        var (profile, error) = await gameService.UploadProfileAvatarAsync(
            User.GetPlayerId(),
            stream,
            file.ContentType,
            file.Length,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("profile/background")]
    [RequestSizeLimit(ProfileBackgroundUploadLimits.MaxBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PlayerProfileResponse>> UploadBackground(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose an image file to upload." });
        }

        await using var stream = file.OpenReadStream();
        var (profile, error) = await gameService.UploadProfileBackgroundAsync(
            User.GetPlayerId(),
            stream,
            file.ContentType,
            file.Length,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("profile/company-logo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PlayerProfileResponse>> UploadCompanyLogo(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Choose a PNG file to upload." });
        }

        await using var stream = file.OpenReadStream();
        var (profile, error) = await gameService.UploadCompanyLogoAsync(
            User.GetPlayerId(),
            stream,
            file.ContentType,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("profile/company-logo/generate")]
    public async Task<ActionResult<CompanyLogoGenerationActionResponse>> EnqueueCompanyLogoGeneration(
        CancellationToken ct)
    {
        var (result, error) = await gameService.EnqueueCompanyLogoGenerationAsync(User.GetPlayerId(), ct);
        if (result is null)
        {
            return NotFound();
        }

        if (error is not null)
        {
            return BadRequest(new { message = error, generation = result.Generation });
        }

        return Accepted(result);
    }

    [HttpGet("profile/company-logo/generation")]
    public async Task<ActionResult<CompanyLogoGenerationStatusDto>> GetCompanyLogoGeneration(CancellationToken ct)
    {
        var status = await gameService.GetCompanyLogoGenerationStatusAsync(User.GetPlayerId(), ct);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpDelete("profile/background")]
    public async Task<ActionResult<PlayerProfileResponse>> RemoveBackground(CancellationToken ct)
    {
        var (profile, error) = await gameService.RemoveProfileBackgroundAsync(User.GetPlayerId(), ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("profile/user/{username}")]
    public async Task<ActionResult<PlayerProfileResponse>> GetProfile(string username, CancellationToken ct)
    {
        var profile = await gameService.GetProfileByUsernameAsync(username, User.GetPlayerId(), ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<PlayerProfileResponse>> UpdateProfile(
        UpdatePlayerProfileRequest request,
        CancellationToken ct)
    {
        var (profile, error) = await gameService.UpdateProfileAsync(User.GetPlayerId(), request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("company-name")]
    public async Task<ActionResult<CompanyNameActionResponse>> UpdateCompanyName(
        UpdateCompanyNameRequest request,
        CancellationToken ct)
    {
        var (result, error) = await companyNameService.RenameMineAsync(
            User.GetPlayerId(),
            request.CompanyName,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [HttpPost("company-name/regenerate")]
    public async Task<ActionResult<CompanyNameActionResponse>> RegenerateCompanyName(CancellationToken ct)
    {
        var (result, error) = await companyNameService.RegenerateMineNameAsync(User.GetPlayerId(), ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [HttpPost("company-name/listing")]
    public async Task<ActionResult<CompanyNameActionResponse>> ListCompanyNameForSale(
        ListCompanyNameRequest request,
        CancellationToken ct)
    {
        var (result, error) = await companyNameService.CreateListingAsync(
            User.GetPlayerId(),
            request.Price,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [HttpDelete("company-name/listing/{listingId:guid}")]
    public async Task<ActionResult<CompanyNameActionResponse>> CancelCompanyNameListing(
        Guid listingId,
        CancellationToken ct)
    {
        var (result, error) = await companyNameService.CancelListingAsync(
            User.GetPlayerId(),
            listingId,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [HttpGet("friends")]
    public async Task<ActionResult<FriendsListResponse>> GetFriends(CancellationToken ct)
    {
        var friends = await gameService.GetFriendsAsync(User.GetPlayerId(), ct);
        return Ok(friends);
    }

    [HttpPost("friends")]
    public async Task<ActionResult<FriendActionResponse>> AddFriend(
        AddFriendRequest request,
        CancellationToken ct)
    {
        var (success, message) = await gameService.AddFriendByProfileNumberAsync(
            User.GetPlayerId(),
            request.ProfileNumber,
            ct);

        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new FriendActionResponse(true, message));
    }

    [HttpPost("friends/{friendshipId:guid}/accept")]
    public async Task<ActionResult<FriendActionResponse>> AcceptFriend(Guid friendshipId, CancellationToken ct)
    {
        var (success, message) = await gameService.AcceptFriendAsync(User.GetPlayerId(), friendshipId, ct);
        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new FriendActionResponse(true, message));
    }

    [HttpDelete("friends/{friendshipId:guid}")]
    public async Task<ActionResult<FriendActionResponse>> RemoveFriend(Guid friendshipId, CancellationToken ct)
    {
        var (success, message) = await gameService.RemoveFriendAsync(User.GetPlayerId(), friendshipId, ct);
        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new FriendActionResponse(true, message));
    }

    [HttpGet("messages")]
    public async Task<ActionResult<PlayerMessagesResponse>> Messages(CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        return Ok(new PlayerMessagesResponse(await playerMessageService.GetInboxAsync(playerId, ct)));
    }

    [HttpGet("messages/unread-count")]
    public async Task<ActionResult<PlayerUnreadCountResponse>> UnreadCount(CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        var staffCount = await playerMessageService.GetUnreadCountAsync(playerId, ct);
        var peerCount = await peerMessageService.GetUnreadCountAsync(playerId, ct);
        return Ok(new PlayerUnreadCountResponse(staffCount + peerCount));
    }

    [HttpPost("messages/{messageId:guid}/read")]
    public async Task<ActionResult<PlayerMessageDto>> MarkMessageRead(Guid messageId, CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        var (message, error) = await playerMessageService.MarkReadAsync(messageId, playerId, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(message);
    }

    [HttpGet("peer-messages")]
    public async Task<ActionResult<PeerMessagesResponse>> PeerMessages(CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        return Ok(new PeerMessagesResponse(await peerMessageService.GetMailboxAsync(playerId, ct)));
    }

    [HttpPost("peer-messages")]
    public async Task<ActionResult<SendPeerMessageResponse>> SendPeerMessage(
        SendPeerMessageRequest request,
        CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        var (message, error) = await peerMessageService.SendMessageAsync(
            playerId,
            request.ToPlayerId,
            request.Body,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new SendPeerMessageResponse(message!, "Message sent."));
    }

    [HttpPost("peer-messages/{messageId:guid}/read")]
    public async Task<ActionResult<PeerMessageDto>> MarkPeerMessageRead(Guid messageId, CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        var (message, error) = await peerMessageService.MarkReadAsync(messageId, playerId, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(message);
    }

    [HttpGet("staff-contacts")]
    public ActionResult<StaffContactsResponse> StaffContacts() =>
        Ok(new StaffContactsResponse(playerToStaffMessageService.GetStaffContacts()));

    [HttpGet("staff-messages")]
    public async Task<ActionResult<PlayerToStaffMessagesResponse>> StaffMessagesSent(CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        return Ok(new PlayerToStaffMessagesResponse(await playerToStaffMessageService.GetSentByPlayerAsync(playerId, ct)));
    }

    [HttpPost("staff-messages")]
    public async Task<ActionResult<SendPlayerToStaffMessageResponse>> SendStaffMessage(
        SendPlayerToStaffMessageRequest request,
        CancellationToken ct)
    {
        var playerId = User.GetPlayerId();
        var (message, error) = await playerToStaffMessageService.SendFromPlayerAsync(
            playerId,
            request.ToStaffUsername,
            request.Body,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new SendPlayerToStaffMessageResponse(message!, "Message sent to staff."));
    }
}
