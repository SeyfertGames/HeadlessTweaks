using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkyFrost.Base;

namespace HeadlessTweaks
{
    partial class MessageCommands
    {
        public partial class Commands
        {
            // Set permission level for a user
            // Usage: /setPerm [user id] [level]
            // level is PermissionLevel enum or int value
            // User can not set their own permission level
            // Target permission must be lower than or equal to your own

            [Command(
                "setPerm",
                "Sets a user's permission level",
                "Moderation",
                PermissionLevel.Moderator,
                usage: "[user] [level] [?world1] [?world2] ..."
            )]
            public static async Task SetPerm(UserMessages userMessages, Message msg, string[] args)
            {
                if (args.Length < 2)
                {
                    _ = userMessages.SendTextMessage(
                        "Usage: /setperm [user] [level] [?world1] [?world2] ..."
                    );
                    return;
                }

                string userId = await TryGetUserId(args[0]);
                if (userId == null)
                {
                    _ = userMessages.SendTextMessage($"Could not find user {args[0]}");
                    return;
                }

                if (userId == msg.SenderId)
                {
                    _ = userMessages.SendTextMessage("You can not set your own permission level");
                    return;
                }

                if (!Enum.TryParse(args[1], true, out PermissionLevel levelEnum))
                {
                    _ = userMessages.SendTextMessage($"Invalid permission level \"{args[1]}\"");
                    return;
                }

                bool hasWorldArgs = args.Length >= 3;

                if (!hasWorldArgs)
                {
                    if (GetUserPermissionLevel(userId) > GetUserPermissionLevel(msg.SenderId))
                    {
                        _ = userMessages.SendTextMessage(
                            "You can not set a user's permission level who is higher than you"
                        );
                        return;
                    }
                    if (GetUserPermissionLevel(msg.SenderId) < levelEnum)
                    {
                        _ = userMessages.SendTextMessage(
                            "You can not set a user's permission level higher than yours"
                        );
                        return;
                    }

                    var levels = HeadlessTweaks.PermissionLevels.GetValue();
                    levels[userId] = levelEnum;
                    HeadlessTweaks.PermissionLevels.SetValueAndSave(levels);
                    _ = userMessages.SendTextMessage("Global permission level set to " + levelEnum);
                }
                else
                {
                    var resolvedWorlds = new List<FrooxEngine.World>();
                    for (int i = 2; i < args.Length; i++)
                    {
                        var world = GetWorld(userMessages, args[i]);
                        if (world == null)
                            return;
                        resolvedWorlds.Add(world);
                    }

                    foreach (var world in resolvedWorlds)
                    {
                        var senderLevel = GetUserPermissionLevelForWorld(msg.SenderId, world);
                        var targetLevel = GetUserPermissionLevelForWorld(userId, world);
                        if (targetLevel > senderLevel)
                        {
                            _ = userMessages.SendTextMessage(
                                $"You can not set a user's permission level who is higher than you in \"{world.RawName}\""
                            );
                            return;
                        }
                        if (senderLevel < levelEnum)
                        {
                            _ = userMessages.SendTextMessage(
                                $"You can not set a user's permission level higher than yours in \"{world.RawName}\""
                            );
                            return;
                        }
                    }

                    var scopedPerms = HeadlessTweaks.WorldScopedPermissions.GetValue();
                    if (!scopedPerms.TryGetValue(userId, out var userScoped))
                    {
                        userScoped = [];
                        scopedPerms[userId] = userScoped;
                    }

                    var sb = new StringBuilder();
                    sb.Append("World-scoped permission set to ").Append(levelEnum).AppendLine(":");

                    foreach (var world in resolvedWorlds)
                    {
                        if (levelEnum == PermissionLevel.None)
                            userScoped.Remove(world.RawName);
                        else
                            userScoped[world.RawName] = levelEnum;

                        sb.Append("  ").AppendLine(world.RawName);
                    }

                    if (userScoped.Count == 0)
                        scopedPerms.Remove(userId);

                    HeadlessTweaks.WorldScopedPermissions.SetValueAndSave(scopedPerms);
                    _ = userMessages.SendTextMessage(sb.ToString().TrimEnd());
                }
            }

            // Get user permission level
            // Usage: /getPerm [?user id]

            [Command(
                "getPerm",
                "Get user permission level",
                "Moderation",
                PermissionLevel.Moderator,
                usage: "[?user]"
            )]
            public static async Task GetPerm(UserMessages userMessages, Message msg, string[] args)
            {
                string userId = msg.SenderId;
                if (args.Length >= 1)
                {
                    userId = await TryGetUserId(args[0]);
                    if (userId == null)
                    {
                        _ = userMessages.SendTextMessage($"Could not find user \"{args[0]}\"");
                        return;
                    }
                }

                var globalLevel = HeadlessTweaks
                    .PermissionLevels.GetValue()
                    .FirstOrDefault(x => x.Key == userId)
                    .Value;

                var sb = new StringBuilder();
                sb.Append(userId).Append(" has a global permission level of ").Append(globalLevel);

                var worldScoped = HeadlessTweaks.WorldScopedPermissions.GetValue();
                if (worldScoped.TryGetValue(userId, out var scopedWorlds) && scopedWorlds.Count > 0)
                {
                    sb.AppendLine().Append("World-scoped permissions:");
                    foreach (var kvp in scopedWorlds.OrderBy(x => x.Key))
                        sb.AppendLine().Append("  ").Append(kvp.Key).Append(": ").Append(kvp.Value);
                }

                _ = userMessages.SendTextMessage(sb.ToString());
            }
        }
    }
}
