using Microsoft.EntityFrameworkCore;
using Theexonet.Infrastructure.Data;

namespace Theexonet.Infrastructure;

public static class DatabaseSchemaUpdater
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PasswordResetTokens" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "TokenHash" text NOT NULL,
                "ExpiresAt" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "Used" boolean NOT NULL DEFAULT FALSE,
                CONSTRAINT "PK_PasswordResetTokens" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PasswordResetTokens_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PasswordResetTokens_PlayerId" ON "PasswordResetTokens" ("PlayerId");
            CREATE INDEX IF NOT EXISTS "IX_PasswordResetTokens_TokenHash" ON "PasswordResetTokens" ("TokenHash");
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "LastProcessedUtcDate" date;
            UPDATE "Players"
            SET "LastProcessedUtcDate" = CAST("CreatedAt" AT TIME ZONE 'UTC' AS date)
            WHERE "LastProcessedUtcDate" IS NULL;
            ALTER TABLE "MarketPriceHistory" ADD COLUMN IF NOT EXISTS "UtcDate" date;
            ALTER TABLE "MarketPriceHistory" ADD COLUMN IF NOT EXISTS "ChangePct" numeric NOT NULL DEFAULT 0;
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileMood" text NOT NULL DEFAULT 'Ready to mine.';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileAboutMe" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileTheme" text NOT NULL DEFAULT 'classic';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileMusic" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileInterests" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileDiscord" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileBluesky" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileTwitter" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileYoutube" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileFacebook" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileNumber" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileImageUrl" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileImageRevision" integer NOT NULL DEFAULT 0;
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileAvatarPreset" text NOT NULL DEFAULT 'neutral';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileGender" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfilePreferredPronouns" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileLocale" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileBackgroundUrl" text NOT NULL DEFAULT '';
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileBackgroundRevision" integer NOT NULL DEFAULT 0;
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "Birthday" date;
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "LastBirthdayBonusYear" integer;
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Players_ProfileNumber"
                ON "Players" ("ProfileNumber") WHERE "ProfileNumber" <> '';
            CREATE TABLE IF NOT EXISTS "Friendships" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "FriendId" uuid NOT NULL,
                "Status" text NOT NULL DEFAULT 'pending',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "AcceptedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_Friendships" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Friendships_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Friendships_Players_FriendId" FOREIGN KEY ("FriendId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_Friendships_PlayerId" ON "Friendships" ("PlayerId");
            CREATE INDEX IF NOT EXISTS "IX_Friendships_FriendId" ON "Friendships" ("FriendId");
            CREATE TABLE IF NOT EXISTS "ReporterFriendships" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "ReporterSlug" text NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_ReporterFriendships" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ReporterFriendships_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReporterFriendships_PlayerId_ReporterSlug"
                ON "ReporterFriendships" ("PlayerId", "ReporterSlug");
            CREATE INDEX IF NOT EXISTS "IX_ReporterFriendships_PlayerId" ON "ReporterFriendships" ("PlayerId");
            CREATE TABLE IF NOT EXISTS "DataMigrations" (
                "Id" text NOT NULL,
                "AppliedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_DataMigrations" PRIMARY KEY ("Id")
            );
            CREATE TABLE IF NOT EXISTS "ProfileFlags" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "FlaggedByUsername" text NOT NULL DEFAULT '',
                "Comment" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "ResolvedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_ProfileFlags" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ProfileFlags_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_ProfileFlags_PlayerId" ON "ProfileFlags" ("PlayerId");
            CREATE TABLE IF NOT EXISTS "PlayerBans" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "BanLevel" text NOT NULL DEFAULT '',
                "BannedByUsername" text NOT NULL DEFAULT '',
                "Reason" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "ExpiresAt" timestamp with time zone NULL,
                "LiftedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PlayerBans" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PlayerBans_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PlayerBans_PlayerId" ON "PlayerBans" ("PlayerId");
            CREATE TABLE IF NOT EXISTS "BanAppeals" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "BanId" uuid NULL,
                "Message" text NOT NULL DEFAULT '',
                "Status" text NOT NULL DEFAULT 'pending',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "ReviewedAt" timestamp with time zone NULL,
                "ReviewedByUsername" text NOT NULL DEFAULT '',
                CONSTRAINT "PK_BanAppeals" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_BanAppeals_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_BanAppeals_PlayerId" ON "BanAppeals" ("PlayerId");
            CREATE INDEX IF NOT EXISTS "IX_BanAppeals_Status" ON "BanAppeals" ("Status");
            CREATE TABLE IF NOT EXISTS "StaffMessages" (
                "Id" uuid NOT NULL,
                "FromUsername" text NOT NULL DEFAULT '',
                "ToUsername" text NOT NULL DEFAULT '',
                "Body" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "ReadAt" timestamp with time zone NULL,
                CONSTRAINT "PK_StaffMessages" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_StaffMessages_ToUsername" ON "StaffMessages" ("ToUsername");
            CREATE INDEX IF NOT EXISTS "IX_StaffMessages_FromUsername" ON "StaffMessages" ("FromUsername");
            CREATE INDEX IF NOT EXISTS "IX_StaffMessages_CreatedAt" ON "StaffMessages" ("CreatedAt");
            CREATE TABLE IF NOT EXISTS "PlayerMessages" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "FromStaffUsername" text NOT NULL DEFAULT '',
                "Body" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "ReadAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PlayerMessages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PlayerMessages_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PlayerMessages_PlayerId" ON "PlayerMessages" ("PlayerId");
            CREATE INDEX IF NOT EXISTS "IX_PlayerMessages_CreatedAt" ON "PlayerMessages" ("CreatedAt");
            CREATE TABLE IF NOT EXISTS "PeerMessages" (
                "Id" uuid NOT NULL,
                "FromPlayerId" uuid NOT NULL,
                "ToPlayerId" uuid NOT NULL,
                "Body" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "ReadAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PeerMessages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PeerMessages_Players_FromPlayerId" FOREIGN KEY ("FromPlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PeerMessages_Players_ToPlayerId" FOREIGN KEY ("ToPlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PeerMessages_FromPlayerId" ON "PeerMessages" ("FromPlayerId");
            CREATE INDEX IF NOT EXISTS "IX_PeerMessages_ToPlayerId" ON "PeerMessages" ("ToPlayerId");
            CREATE INDEX IF NOT EXISTS "IX_PeerMessages_CreatedAt" ON "PeerMessages" ("CreatedAt");
            CREATE TABLE IF NOT EXISTS "PlayerToStaffMessages" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "ToStaffUsername" text NOT NULL DEFAULT '',
                "Body" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "ReadAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PlayerToStaffMessages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PlayerToStaffMessages_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PlayerToStaffMessages_PlayerId" ON "PlayerToStaffMessages" ("PlayerId");
            CREATE INDEX IF NOT EXISTS "IX_PlayerToStaffMessages_ToStaffUsername" ON "PlayerToStaffMessages" ("ToStaffUsername");
            CREATE INDEX IF NOT EXISTS "IX_PlayerToStaffMessages_CreatedAt" ON "PlayerToStaffMessages" ("CreatedAt");
            CREATE TABLE IF NOT EXISTS "FlaggedMessages" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NULL,
                "Channel" text NOT NULL DEFAULT '',
                "SourceMessageId" uuid NOT NULL,
                "FromLabel" text NOT NULL DEFAULT '',
                "ToLabel" text NOT NULL DEFAULT '',
                "Body" text NOT NULL DEFAULT '',
                "MatchedTerms" text NOT NULL DEFAULT '',
                "Status" text NOT NULL DEFAULT 'pending',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "ReviewedAt" timestamp with time zone NULL,
                "ReviewedByUsername" text NOT NULL DEFAULT '',
                CONSTRAINT "PK_FlaggedMessages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_FlaggedMessages_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_FlaggedMessages_PlayerId" ON "FlaggedMessages" ("PlayerId");
            CREATE INDEX IF NOT EXISTS "IX_FlaggedMessages_Status" ON "FlaggedMessages" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_FlaggedMessages_CreatedAt" ON "FlaggedMessages" ("CreatedAt");
            CREATE TABLE IF NOT EXISTS "PlayerWarnings" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "FlaggedMessageId" uuid NULL,
                "Reason" text NOT NULL DEFAULT '',
                "IssuedByUsername" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_PlayerWarnings" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PlayerWarnings_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PlayerWarnings_FlaggedMessages_FlaggedMessageId" FOREIGN KEY ("FlaggedMessageId")
                    REFERENCES "FlaggedMessages" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_PlayerWarnings_PlayerId" ON "PlayerWarnings" ("PlayerId");
            CREATE INDEX IF NOT EXISTS "IX_PlayerWarnings_CreatedAt" ON "PlayerWarnings" ("CreatedAt");
            ALTER TABLE "PlayerWarnings" ADD COLUMN IF NOT EXISTS "ExpiresAt" timestamp with time zone NULL;
            UPDATE "PlayerWarnings"
            SET "ExpiresAt" = "CreatedAt" + INTERVAL '30 days'
            WHERE "ExpiresAt" IS NULL;
            CREATE INDEX IF NOT EXISTS "IX_PlayerWarnings_ExpiresAt" ON "PlayerWarnings" ("ExpiresAt");
            ALTER TABLE "PlayerWarnings" ADD COLUMN IF NOT EXISTS "AcknowledgedAt" timestamp with time zone NULL;
            UPDATE "PlayerWarnings"
            SET "AcknowledgedAt" = "CreatedAt"
            WHERE "AcknowledgedAt" IS NULL;
            CREATE INDEX IF NOT EXISTS "IX_PlayerWarnings_AcknowledgedAt" ON "PlayerWarnings" ("AcknowledgedAt");
            CREATE TABLE IF NOT EXISTS "SpecialEvents" (
                "Id" uuid NOT NULL,
                "Title" text NOT NULL DEFAULT '',
                "Message" text NOT NULL DEFAULT '',
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "StartsAt" timestamp with time zone NULL,
                "EndsAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_SpecialEvents" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_SpecialEvents_IsActive" ON "SpecialEvents" ("IsActive");
            CREATE TABLE IF NOT EXISTS "SpecialEventRewards" (
                "Id" uuid NOT NULL,
                "EventId" uuid NOT NULL,
                "ItemType" text NOT NULL DEFAULT '',
                "Amount" numeric NOT NULL DEFAULT 0,
                CONSTRAINT "PK_SpecialEventRewards" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SpecialEventRewards_SpecialEvents_EventId" FOREIGN KEY ("EventId")
                    REFERENCES "SpecialEvents" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_SpecialEventRewards_EventId" ON "SpecialEventRewards" ("EventId");
            CREATE TABLE IF NOT EXISTS "SpecialEventClaims" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "EventId" uuid NOT NULL,
                "ClaimedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_SpecialEventClaims" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SpecialEventClaims_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SpecialEventClaims_SpecialEvents_EventId" FOREIGN KEY ("EventId")
                    REFERENCES "SpecialEvents" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SpecialEventClaims_PlayerId_EventId"
                ON "SpecialEventClaims" ("PlayerId", "EventId");
            CREATE INDEX IF NOT EXISTS "IX_SpecialEventClaims_EventId" ON "SpecialEventClaims" ("EventId");
            ALTER TABLE "SpecialEvents" ADD COLUMN IF NOT EXISTS "ChallengeType" text NOT NULL DEFAULT 'AdvanceDay';
            ALTER TABLE "SpecialEvents" ADD COLUMN IF NOT EXISTS "ChallengeTarget" integer NOT NULL DEFAULT 1;
            ALTER TABLE "SpecialEvents" ADD COLUMN IF NOT EXISTS "ChallengeDetail" text NOT NULL DEFAULT '';
            CREATE TABLE IF NOT EXISTS "SpecialEventProgress" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "EventId" uuid NOT NULL,
                "ProgressCount" integer NOT NULL DEFAULT 0,
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_SpecialEventProgress" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SpecialEventProgress_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SpecialEventProgress_SpecialEvents_EventId" FOREIGN KEY ("EventId")
                    REFERENCES "SpecialEvents" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SpecialEventProgress_PlayerId_EventId"
                ON "SpecialEventProgress" ("PlayerId", "EventId");
            CREATE INDEX IF NOT EXISTS "IX_SpecialEventProgress_EventId" ON "SpecialEventProgress" ("EventId");
            CREATE TABLE IF NOT EXISTS "SpecialEventAnnouncements" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "EventId" uuid NOT NULL,
                "AnnouncedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_SpecialEventAnnouncements" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SpecialEventAnnouncements_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SpecialEventAnnouncements_SpecialEvents_EventId" FOREIGN KEY ("EventId")
                    REFERENCES "SpecialEvents" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SpecialEventAnnouncements_PlayerId_EventId"
                ON "SpecialEventAnnouncements" ("PlayerId", "EventId");
            CREATE INDEX IF NOT EXISTS "IX_SpecialEventAnnouncements_EventId" ON "SpecialEventAnnouncements" ("EventId");
            ALTER TABLE "SpecialEvents" ADD COLUMN IF NOT EXISTS "SaleBonusPercent" numeric NOT NULL DEFAULT 0;
            ALTER TABLE "SpecialEvents" ADD COLUMN IF NOT EXISTS "TradeBonusPercent" numeric NOT NULL DEFAULT 0;
            CREATE TABLE IF NOT EXISTS "CompanyNameLimbo" (
                "Id" uuid NOT NULL,
                "NormalizedName" text NOT NULL,
                "DisplayName" text NOT NULL,
                "AvailableAfter" timestamp with time zone NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "PK_CompanyNameLimbo" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_CompanyNameLimbo_NormalizedName" ON "CompanyNameLimbo" ("NormalizedName");
            CREATE INDEX IF NOT EXISTS "IX_CompanyNameLimbo_AvailableAfter" ON "CompanyNameLimbo" ("AvailableAfter");
            ALTER TABLE "CompanyNameLimbo" ADD COLUMN IF NOT EXISTS "PlayerId" uuid NULL;
            CREATE INDEX IF NOT EXISTS "IX_CompanyNameLimbo_PlayerId" ON "CompanyNameLimbo" ("PlayerId");
            DO $schema$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_CompanyNameLimbo_Players_PlayerId') THEN
                    ALTER TABLE "CompanyNameLimbo"
                        ADD CONSTRAINT "FK_CompanyNameLimbo_Players_PlayerId"
                        FOREIGN KEY ("PlayerId") REFERENCES "Players" ("Id") ON DELETE SET NULL;
                END IF;
            END
            $schema$;
            CREATE TABLE IF NOT EXISTS "CompanyNameListings" (
                "Id" uuid NOT NULL,
                "SellerPlayerId" uuid NOT NULL,
                "SellerMineId" uuid NOT NULL,
                "CompanyName" text NOT NULL,
                "NormalizedName" text NOT NULL,
                "Price" numeric NOT NULL,
                "Status" text NOT NULL DEFAULT 'active',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "SoldAt" timestamp with time zone NULL,
                "BuyerPlayerId" uuid NULL,
                CONSTRAINT "PK_CompanyNameListings" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_CompanyNameListings_Players_SellerPlayerId" FOREIGN KEY ("SellerPlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_CompanyNameListings_NormalizedName" ON "CompanyNameListings" ("NormalizedName");
            CREATE INDEX IF NOT EXISTS "IX_CompanyNameListings_Status" ON "CompanyNameListings" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_CompanyNameListings_SellerPlayerId" ON "CompanyNameListings" ("SellerPlayerId");
            ALTER TABLE "GameWorld" ADD COLUMN IF NOT EXISTS "TradeMarketValue" numeric NOT NULL DEFAULT 0;
            CREATE TABLE IF NOT EXISTS "TradeAuctions" (
                "Id" uuid NOT NULL,
                "SellerPlayerId" uuid NOT NULL,
                "Category" integer NOT NULL,
                "ItemType" text NOT NULL,
                "Quantity" numeric NOT NULL,
                "StartPrice" numeric NOT NULL,
                "CurrentBid" numeric NULL,
                "HighBidderPlayerId" uuid NULL,
                "DurationMinutes" integer NOT NULL,
                "EndsAt" timestamp with time zone NULL,
                "Status" text NOT NULL DEFAULT 'open',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "CompletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_TradeAuctions" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TradeAuctions_Players_SellerPlayerId" FOREIGN KEY ("SellerPlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TradeAuctions_Players_HighBidderPlayerId" FOREIGN KEY ("HighBidderPlayerId")
                    REFERENCES "Players" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_TradeAuctions_Status" ON "TradeAuctions" ("Status");
            CREATE INDEX IF NOT EXISTS "IX_TradeAuctions_SellerPlayerId" ON "TradeAuctions" ("SellerPlayerId");
            CREATE INDEX IF NOT EXISTS "IX_TradeAuctions_EndsAt" ON "TradeAuctions" ("EndsAt");
            ALTER TABLE "Mines" ADD COLUMN IF NOT EXISTS "CompanyLogoUrl" text NOT NULL DEFAULT '';
            ALTER TABLE "Mines" ADD COLUMN IF NOT EXISTS "CompanyLogoRevision" integer NOT NULL DEFAULT 0;
            ALTER TABLE "Mines" ADD COLUMN IF NOT EXISTS "CompanyLogoIsCustom" boolean NOT NULL DEFAULT FALSE;
            CREATE TABLE IF NOT EXISTS "CompanyLogoQueue" (
                "Id" uuid NOT NULL,
                "MineId" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "Status" text NOT NULL DEFAULT 'queued',
                "Source" text NOT NULL DEFAULT 'user',
                "Error" text NULL,
                "RequestedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "StartedAt" timestamp with time zone NULL,
                "CompletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_CompanyLogoQueue" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_CompanyLogoQueue_Mines_MineId" FOREIGN KEY ("MineId")
                    REFERENCES "Mines" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CompanyLogoQueue_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_CompanyLogoQueue_MineId_Status" ON "CompanyLogoQueue" ("MineId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_CompanyLogoQueue_RequestedAt" ON "CompanyLogoQueue" ("RequestedAt");
            CREATE TABLE IF NOT EXISTS "AiImageQueue" (
                "Id" uuid NOT NULL,
                "Kind" text NOT NULL,
                "Payload" text NOT NULL DEFAULT '{{}}',
                "Status" text NOT NULL DEFAULT 'queued',
                "Source" text NOT NULL DEFAULT '',
                "Error" text NULL,
                "RequestedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "StartedAt" timestamp with time zone NULL,
                "CompletedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_AiImageQueue" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_AiImageQueue_Status_RequestedAt" ON "AiImageQueue" ("Status", "RequestedAt");
            CREATE INDEX IF NOT EXISTS "IX_AiImageQueue_Kind" ON "AiImageQueue" ("Kind");
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "LastSeenAtUtc" timestamp with time zone;
            CREATE INDEX IF NOT EXISTS "IX_Players_LastSeenAtUtc" ON "Players" ("LastSeenAtUtc");
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileBirthdayPublic" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "ProfileAgePublic" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "AdminTestingModeEnabled" boolean NOT NULL DEFAULT false;
            ALTER TABLE "PlayerMessages" ADD COLUMN IF NOT EXISTS "HiddenForPlayerAt" timestamp with time zone NULL;
            ALTER TABLE "PeerMessages" ADD COLUMN IF NOT EXISTS "HiddenForSenderAt" timestamp with time zone NULL;
            ALTER TABLE "PeerMessages" ADD COLUMN IF NOT EXISTS "HiddenForRecipientAt" timestamp with time zone NULL;
            ALTER TABLE "PlayerToStaffMessages" ADD COLUMN IF NOT EXISTS "HiddenForPlayerAt" timestamp with time zone NULL;
            ALTER TABLE "PlayerToStaffMessages" ADD COLUMN IF NOT EXISTS "HiddenForStaffAt" timestamp with time zone NULL;
            ALTER TABLE "Players" ADD COLUMN IF NOT EXISTS "JobApplicationCompletedAt" timestamp with time zone NULL;
            CREATE TABLE IF NOT EXISTS "PlayerJobHistory" (
                "Id" uuid NOT NULL,
                "PlayerId" uuid NOT NULL,
                "JobSlug" text NOT NULL,
                "JobTitle" text NOT NULL,
                "IsCurrent" boolean NOT NULL DEFAULT false,
                "StartedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "EndedAtUtc" timestamp with time zone NULL,
                CONSTRAINT "PK_PlayerJobHistory" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_PlayerJobHistory_Players_PlayerId" FOREIGN KEY ("PlayerId")
                    REFERENCES "Players" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PlayerJobHistory_PlayerId" ON "PlayerJobHistory" ("PlayerId");
            CREATE INDEX IF NOT EXISTS "IX_PlayerJobHistory_PlayerId_IsCurrent" ON "PlayerJobHistory" ("PlayerId", "IsCurrent");
            """,
            cancellationToken);
    }
}
