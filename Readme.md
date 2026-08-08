# RecommendMe

RecommendMe is an Emby Server plugin that lets people recommend movies, television, music, and other supported library items to one another from inside Emby.

Recommendations are added to a native Emby collection for the recipient, so they appear alongside the recipient's other library content. The plugin also keeps a history of who recommended each item, who received it, when it was sent, and whether the recommendation was private.

## Features

- Search the Emby library and recommend an item to another user.
- Recommend an item to yourself for later viewing or listening.
- Store recommendations in a dedicated native Emby collection for each recipient.
- Browse recommendation history with sortable and filterable columns.
- Mark recommendations as private so only the sender, recipient, and administrators can see them in history.
- Notify recipients through their active Emby sessions when a recommendation arrives.
- Prevent recommendations for items the recipient has already watched.
- Automatically remove watched recommendations from the recipient's collection.
- Reconcile watched recommendations through an optional Emby scheduled task.

## User controls

Each user can decide which permitted senders they want to receive recommendations from. They can block an individual sender entirely or opt out of particular media types from that sender.

User preferences can only narrow the permissions configured by an administrator; they cannot grant access that the administrator has not allowed.

## Administrator controls

Administrators can:

- Suspend all RecommendMe access for an individual user.
- Choose whether a user can recommend to everyone, nobody, selected users, or members of shared groups.
- Create groups and manage their membership.
- Select a user's policy as the template applied when a new user is first encountered.
- Choose which media types can be recommended server-wide.
- Configure the prefix and suffix used for recommendation collection names.
- Enable watched-item prevention and automatic watched-item removal.
- Choose whether user and group lists are expanded or search-limited in the administration UI.

Administrative pages and commands are protected by server-side administrator checks.

## Recommendation history and privacy

Senders and recipients can always see their own recommendation records while they have access to the plugin. Other users can only see non-private records when their current administrative scope includes both participants. Private records are restricted to their sender and recipient.

Emby administrators have audit access to the complete recommendation history.

## Data storage

RecommendMe stores its settings and history as JSON beneath Emby's program-data directory:

```text
data/RecommendMe/
```

This includes administrator settings, user receiving preferences, recommendation history, and the registry of plugin-managed Emby collections. Writes use temporary files and backups to reduce the risk of losing an existing data file if a write is interrupted.
