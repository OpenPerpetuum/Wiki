---
title: "Features"
description: "Everything a player can do in the game, organized by system."
---

# What You Can Do in the Game

Every player action, organized by system. The command name is the server-side
identifier — the client presents these actions in its own UI. Per-action prose
lives on the per-system pages.

## Systems

| System | Page | What it covers |
|---|---|---|
| Getting started | [getting-started](/features/getting-started/) | creating/selecting characters, first robot, account basics |
| Movement | [movement](/features/movement/) | zones, docking, queue, teleports, spark jumps, gates |
| Gathering | [gathering](/features/gathering/) | scanning, harvesting, resource collection |
| Combat | [combat](/features/combat/) | fighting, alarms, kill reports, SOS |
| Robots & fitting | [robots](/features/robots/) | selecting robots, modules, ammo, presets, tints |
| Research | [research](/features/research/) | extensions (skill tree), EP, tech tree |
| Production | [production](/features/production/) | production lines, refine, repair, reprocess, prototypes, CPRG |
| Market & trade | [market](/features/market/) | market orders, direct trades, item shop, credits |
| Groups | [groups](/features/groups/) | corporations, alliances, gangs (fleets) |
| Power base stations | [pbs](/features/pbs/) | PBS structures, territories, connections |
| Missions | [missions](/features/missions/) | field missions, agents, terminals |
| Transport | [transport](/features/transport/) | shipping items between bases |
| Items & inventory | [items](/features/items/) | containers, stacking, item names, redeemables, goodie packs |
| Social | [social](/features/social/) | mail, channels, friends, standings, yellow pages, news, polls |
| Intrusion | [intrusion](/features/intrusion/) | the NPC-site assault mode |
| Proximity probes | [probes](/features/probes/) | zone surveillance probes |
| Server & reference | [server](/features/server/) | server info, high scores, reference data, purchases |

### Reference

| Page | What it covers |
|---|---|
| [Client UI overview](/features/ui/) | The EVE-style interface: top bar, Deploy/Dock button, action categories, undocked status panel, and the conventions used across this wiki |

Entity stat tables live in the generated [Content](/content/) section.

## Complete Action Inventory

### Getting started & account

| Action | Command |
|---|---|
| Create a character | `characterCreate` |
| Select (play as) a character | `characterSelect` |
| Delete a character | `characterDelete` |
| Rename a character | `characterRename` |
| Check whether a name is available | `characterCheckNick` |
| Request the starter robot | `requestStarterRobot` |
| Request the starter box | `requestInfiniteBox` |
| New-character setup data | `characterWizardData` |
| View my profile | `characterGetMyProfile` |
| View other characters' profiles | `characterGetProfiles` |
| Search for characters | `characterSearch` |
| List characters | `characterList` |
| Get/set character settings | `characterSettingsGet`, `characterSettingsSet` |
| Set avatar | `characterSetAvatar` |
| Set mood message | `characterSetMoodmessage` |
| Set a private note on a character | `characterSetNote` / `characterGetNote` |
| Set / clear home base | `characterSetHomeBase`, `characterClearHomeBase` |
| Transfer credits between own characters | `characterTransferCredit` |
| Block/allow trades on a character | `characterSetBlockTrades` |
| Change account e-mail / password | `changeSessionEmail`, `changeSessionPassword` |
| View EP-for-activity history | `accountEpForActivityHistory` |
| View account transaction history | `accountGetTransactionHistory` |
| Sign out / connection housekeeping | `signOut`, `connectionStart`, `connectionEnd`, `ping`, `quit`, `isOnline`, `transferData`, `welcome` |
| Who is online | `getCharactersOnline`, `getAccountsWithCharacters` |
| Character histories | `characterNickHistory`, `characterCorporationHistory`, `characterTransactionHistory`, `characterListNpcDeath` |

### Movement & zones

| Action | Command |
|---|---|
| Dock at a base / PBS | `dock` |
| Undock | `undock` |
| Force dock (emergency) | `forceDock` |
| Check zone entry queue | `zoneGetQueueInfo` |
| Set queue length preference | `zoneSetQueueLength` |
| Cancel waiting in the queue | `zoneCancelEnterQueue` |
| Get zone info | `getZoneInfo` |
| List sectors of a zone | `zoneSectorList` |
| List zone buildings | `zoneGetBuildings` |
| Send SOS (call for help) | `zoneSOS` |
| List available teleports | `teleportList` |
| Use a teleport | `teleportUse` |
| Teleport to a zone object | `teleportToZoneObject` |
| List teleport channels | `teleportGetChannelList` |
| Query world-wide teleport channels | `teleportQueryWorldChannels` |
| List spark charges | `sparkList` |
| Change active spark | `sparkChange` |
| Remove a spark | `sparkRemove` |
| Unlock a spark | `sparkUnlock` |
| List / set / use / delete spark teleports | `sparkTeleportList`, `sparkTeleportSet`, `sparkTeleportUse`, `sparkTeleportDelete` |
| Rename a gate | `gateSetName` |
| Base info / ownership / facilities / my items at base | `baseGetInfo`, `baseGetOwnershipInfo`, `baseListFacilities`, `baseGetMyItems` |
| Set base docking rights | `baseSetDockingRights` |

### Gathering & scanning

| Action | Command |
|---|---|
| Scan the zone (ore/plant/deployable scan) | `zoneUploadScanResult` |
| List my mineral scan results | `mineralScanResultList` |
| Create an item from a scan result | `mineralScanResultCreateItem` |
| Delete a scan result | `mineralScanResultDelete` |
| Move scan results between containers | `mineralScanResultMove` |
| Upload scan data from an item | `mineralScanResultUploadFromItem` |
| Count items on a zone (visibility check) | `itemCountOnZone`, `itemCount` |
| Kiosk: view / submit items | `kioskInfo`, `kioskSubmitItem` |
| Use an item (generic — incl. harvestables) | `useItem` |

### Combat

| Action | Command |
|---|---|
| Raise / drop combat alarm | `alarmStart` (alarm end is server-driven) |
| View my kill reports | `getMyKillReports` |
| View my high scores | `getMyHighScores` |
| View server high scores | `getHighScores` |

### Robots & fitting

| Action | Command |
|---|---|
| Select active robot | `selectActiveRobot` |
| Empty a robot (unload all) | `robotEmpty` |
| Get robot info | `getRobotInfo` |
| Get robot fitting info | `getRobotFittingInfo` |
| Equip a module | `equipModule` |
| Remove a module | `removeModule` |
| Swap modules | `changeModule` |
| Equip ammo | `equipAmmo` |
| Swap ammo | `changeAmmo` |
| Unequip ammo | `unEquipAmmo` |
| Set robot tint | `setRobotTint` |
| Save / list / apply / delete fitting presets | `fittingPresetSave`, `fittingPresetList`, `fittingPresetApply`, `fittingPresetDelete` |

### Research (extensions & tech tree)

| Action | Command |
|---|---|
| List extension categories | `extensionCategoryList` |
| Get all extensions | `extensionGetAll` |
| List learned extensions | `extensionLearntList` |
| List prerequisites | `extensionPrerequireList` |
| Buy an extension with points | `extensionBuyForPoints` |
| Buy an EP boost | `extensionBuyEpBoost` |
| Get available points | `extensionGetAvailablePoints` |
| Get point parameters | `extensionGetPointParameters` |
| Extension history | `extensionHistory` |
| Remove one extension level | `extensionRemoveLevel` |
| Reset character extensions | `extensionResetCharacter` |
| Free locked EP | `extensionFreeLockedEp` |
| View EP-for-activity daily log | `epForActivityDailyLog` |
| Get research levels | `getResearchLevels` |
| Tech tree: research a node | `techTreeResearch` |
| Tech tree: unlock a node | `techTreeUnlock` |
| Tech tree: donate | `techTreeDonate` |
| Tech tree: info / logs | `techTreeInfo`, `techTreeGetLogs` |

### Production

| Action | Command |
|---|---|
| List production lines | `productionLineList` |
| Start a line | `productionLineStart` |
| Calibrate a line | `productionLineCalibrate` |
| Set rounds on a line | `productionLineSetRounds` |
| Delete a line | `productionLineDelete` |
| Query next round of a line | `productionQueryLineNextRound` |
| Cancel production | `productionCancel` |
| Refine (query + run) | `productionRefine`, `productionRefineQuery` |
| Repair (query + run) | `productionRepair`, `productionRepairQuery` |
| Reprocess (query + run) | `productionReprocess`, `productionReprocessQuery` |
| Research components (query + run) | `productionResearch`, `productionResearchQuery` |
| Prototype (start + query) | `productionPrototypeStart`, `productionPrototypeQuery` |
| Merge research kits (run + query) | `productionMergeResearchKitsMulti`, `productionMergeResearchKitsMultiQuery` |
| Forge CPRG (run + query) | `productionCPRGForge`, `productionCPRGForgeQuery` |
| CPRG info | `productionCPRGInfo` |
| Get CPRG from a line (get + query) | `productionGetCPRGFromLine`, `productionGetCPRGFromLineQuery` |
| Production insurance: buy / list / query / delete | `productionInsuranceBuy`, `productionInsuranceList`, `productionInsuranceQuery`, `productionInsuranceDelete` |
| List production components | `productionComponentsList` |
| Facility info / description | `productionFacilityInfo`, `productionFacilityDescription` |
| What is in progress (mine / corporation) | `productionInProgress`, `productionInProgressCorporation` |
| Production history | `productionHistory` |
| Production server info | `productionServerInfo` |

### Market & trade

| Action | Command |
|---|---|
| Create a sell order | `marketCreateSellOrder` |
| Create a buy order | `marketCreateBuyOrder` |
| Modify an order | `marketModifyOrder` |
| Cancel an item / order | `marketCancelItem` |
| Buy an item directly | `marketBuyItem` |
| List market items | `marketItemList` |
| List items available to me | `marketAvailableItems` |
| List items in a range | `marketItemsInRange` |
| List my market items | `marketGetMyItems` |
| Market info | `marketGetInfo` |
| Average prices (market / definition / global) | `marketGetAveragePrices`, `marketGetDefinitionAveragePrice`, `marketGlobalAveragePrices` |
| Change market tax / view tax log | `marketTaxChange`, `marketTaxLogList` |
| Begin a trade | `tradeBegin` |
| Set / retract my offer | `tradeSetOffer`, `tradeRetractOffer` |
| Accept a trade | `tradeAccept` |
| Cancel a trade | `tradeCancel` |
| Trade finished / state | `tradeFinished`, `tradeState` |
| Item shop: list / buy | `itemShopList`, `itemShopBuy` |
| Transfer credits to another character | `characterTransferCredit` |

### Groups (corporations, alliances, gangs)

| Action | Command |
|---|---|
| Create a corporation | `corporationCreate` |
| Apply to / invite to a corporation | `corporationApply`, `corporationCharacterInvite` |
| Accept application / list applications | `corporationAcceptApplication`, `corporationListApplications` |
| My applications: list / delete | `corporationListMyApplications`, `corporationDeleteMyApplication` |
| Delete an application (officer) | `corporationDeleteApplication` |
| Reply to invite | `corporationInviteReply` |
| Leave / cancel leave | `corporationLeave`, `corporationCancelLeave` |
| Remove a member / set member role | `corporationRemoveMember`, `corporationSetMemberRole` |
| Drop all my roles | `corporationDropRoles` |
| Set members neutral | `corporationSetMembersNeutral` |
| Corporation info / my info / search | `corporationInfo`, `corporationGetMyInfo`, `corporationSearch` |
| Corporation standings / reputation | `corporationGetMyStandings`, `corporationGetReputation` |
| Donate to / pay out from corporation | `corporationDonate`, `corporationPayOut` |
| Transfer corporation credits | `corporationTransfer` |
| Rename / set color / set info | `corporationRename`, `corporationSetColor`, `corporationSetInfo` |
| Get delegates | `corporationGetDelegates` |
| CEO takeover status / volunteer for CEO | `corporationCEOTakeOverStatus`, `corporationVolunteerForCEO` |
| Corporation votes: start / set topic / list / cast / delete | `corporationVoteStart`, `corporationVoteSetTopic`, `corporationVoteList`, `corporationVoteCast`, `corporationVoteDelete` |
| Bulletin board: start / list / new entries / post / moderate / delete | `corporationBulletinStart`, `corporationBulletinList`, `corporationBulletinNewEntries`, `corporationBulletinEntry`, `corporationBulletinModerate`, `corporationBulletinDelete`, `corporationBulletinEntryDelete`, `corporationBulletinDetails` |
| Hangar: rent / pay rent / rent price / close | `corporationRentHangar`, `corporationHangarPayRent`, `corporationHangarRentPrice`, `corporationHangarClose` |
| Hangar: list (all / on base) | `corporationHangarListAll`, `corporationHangarListOnBase` |
| Hangar: folders (create / delete) | `corporationHangarFolderCreate`, `corporationHangarFolderDelete` |
| Hangar: set access / set name | `corporationHangarSetAccess`, `corporationHangarSetName` |
| Hangar logs: list / set / clear | `corporationHangarLogList`, `corporationHangarLogSet`, `corporationHangarLogClear` |
| Corporation documents: config / create / list / open / delete / transfer / rent / monitor / unmonitor / update body / register list / register set | `corporationDocumentConfig`, `corporationDocumentCreate`, `corporationDocumentList`, `corporationDocumentOpen`, `corporationDocumentDelete`, `corporationDocumentTransfer`, `corporationDocumentRent`, `corporationDocumentMonitor`, `corporationDocumentUnmonitor`, `corporationDocumentUpdateBody`, `corporationDocumentRegisterList`, `corporationDocumentRegisterSet` |
| Histories: log / member roles / corporation roles / roles / name / transactions | `corporationLogHistory`, `corporationMemberRoleHistory`, `corporationRoleHistory`, `corporationNameHistory`, `corporationTransactionHistory` |
| Alliance info / defaults / role history | `allianceGetMyInfo`, `allianceGetDefaults`, `allianceRoleHistory` |
| Gang (fleet): create / delete / info | `gangCreate`, `gangDelete`, `gangInfo` |
| Gang: invite / reply / kick / leave | `gangInvite`, `gangInviteReply`, `gangKick`, `gangLeave` |
| Gang: set leader / set role | `gangSetLeader`, `gangSetRole` |

### Power base stations (PBS) & territories

| Action | Command |
|---|---|
| Get PBS network | `PBSGetNetwork` |
| PBS node info | `PBSNodeInfo` |
| Rename a PBS node | `PBSRenameNode` |
| Make / break a PBS connection | `PBSMakeConnection`, `PBSBreakConnection` |
| Set connection weight | `PBSSetConnectionWeight` |
| Set PBS online state | `PBSSetOnline` |
| Set a PBS effect | `PBSSetEffect` |
| Check deployment (can I deploy here?) | `PBSCheckDeployment` |
| List my territories | `PBSGetTerritories` |
| Set territory visibility | `PBSSetTerritoryVisibility` |
| Set standing limit | `PBSSetStandingLimit` |
| Set reinforce offset | `PBSSetReinforceOffset` |
| Get / set reimburse info | `PBSGetReimburseInfo`, `PBSSetReimburseInfo` |
| Feed items to a PBS | `PBSFeedableInfo`, `PBSFeedItems` |
| Set base deconstruction | `PBSSetBaseDeconstruct` |
| PBS logs | `PBSGetLog` |

### Missions

| Action | Command |
|---|---|
| Start a mission | `missionStart` |
| Start a mission from a zone | `missionStartFromZone` |
| Abort a mission | `missionAbort` |
| Deliver (mission delivery) | `missionDeliver` |
| Mission data / options / supply | `missionData`, `missionGetOptions`, `missionGetSupply` |
| List mission agents | `missionListAgents` |
| List my running missions | `missionListRunning` |
| Mission log | `missionLogList` |
| Add a participant to a mission | `missionPlayerAddsParticipant` |
| Field terminal info | `fieldTerminalInfo` |

### Transport (item shipping)

| Action | Command |
|---|---|
| Submit a transport | `transportAssignmentSubmit` |
| Take a transport (as carrier) | `transportAssignmentTake` |
| List transports | `transportAssignmentList` |
| List transport content | `transportAssignmentListContent` |
| Container info for a transport | `transportAssignmentContainerInfo` |
| Deliver a transport | `transportAssignmentDeliver` |
| Retrieve my transport | `transportAssignmentRetrieve` |
| Cancel / give up a transport | `transportAssignmentCancel`, `transportAssignmentGiveUp` |
| Is a transport running? | `transportAssignmentRunning` |
| Transport log | `transportAssignmentLog` |

### Items & inventory

| Action | Command |
|---|---|
| List a container | `listContainer` |
| Pack items (into a bag/box) | `packItems` |
| Unpack items | `unpackItems` |
| Stack items together | `stackItems`, `stackTo`, `stackSelection` |
| Unstack an amount | `unStackAmount` |
| Relocate items between containers | `relocateItems` |
| Trash items | `trashItems` |
| Rename an item | `setItemName` |
| Use an item | `useItem` |
| Use a lottery item | `useLotteryItem` |
| Redeemables: list / redeem / activate | `redeemableItemList`, `redeemableItemRedeem`, `redeemableItemActivate` |
| Goodie packs: list / redeem | `goodiePackList`, `goodiePackRedeem` |
| Open a gift | `giftOpen` |
| Reimburse an item | `ReimburseItem` |

### Social

| Action | Command |
|---|---|
| Mail: list / open / send / delete / new count / used folders / move to folder / delete folder | `mailList`, `mailOpen`, `mailSend`, `mailDelete`, `mailNewCount`, `mailUsedFolders`, `mailMoveToFolder`, `mailDeleteFolder` |
| Mass mail (corporation): send / list / open / delete / new count | `massMailSend`, `massMailList`, `massMailOpen`, `massMailDelete`, `massMailNewCount` |
| Friends: send / reply / confirm / delete / block / list | `socialFriendRequest`, `socialFriendRequestReply`, `socialConfirmPendingFriendRequest`, `socialDeleteFriend`, `socialBlockFriend`, `socialGetMyList` |
| Chat channels: create / join / leave / talk | `channelCreate`, `channelJoin`, `channelLeave`, `channelTalk` |
| Channels: list (all / mine) | `channelList`, `channelMyList`, `channelListAll` |
| Channels: kick / ban / unban / roles / mute / password / topic / notification | `channelKick`, `channelBan`, `channelRemoveBan`, `channelModifyMemberRole`, `channelGlobalMute`, `channelGetMutedCharacters`, `channelGetBannedMembers`, `channelSetPassword`, `channelSetTopic`, `channelNotification` |
| Direct chat | `chat` |
| Standings: list / history | `standingList`, `standingHistory` |
| Set standing (where allowed) | `setStanding` |
| Default standings (alliances / corporations) | `getStandingForDefaultAlliances`, `getStandingForDefaultCorporations` |
| Yellow pages: get / submit / delete / search | `yellowPagesGet`, `yellowPagesSubmit`, `yellowPagesDelete`, `yellowPagesSearch` |
| News: categories / list / new count | `newsCategory`, `getNews`, `freshNewsCount` |
| Polls: view / answer | `pollGet`, `pollAnswer` |

### Intrusion

| Action | Command |
|---|---|
| Submit an SAP item | `intrusionSAPSubmitItem` |
| Get SAP item info | `intrusionSAPGetItemInfo`, `intrusionSAPItemInfo` |
| Set defense threshold | `intrusionSetDefenseThreshold` |
| Set site effect bonus | `intrusionSiteSetEffectBonus` |
| Upgrade a facility | `intrusionUpgradeFacility` |
| Site info | `getIntrusionSiteInfo` |
| Logs: my / public / my sites / stability | `getIntrusionLog`, `getIntrusionPublicLog`, `getIntrusionMySitesLog`, `getIntrusionStabilityLog` |
| Stability bonus thresholds | `getStabilityBonusThresholds` |
| Set site message | `setIntrusionSiteMessage` |

### Proximity probes

| Action | Command |
|---|---|
| List my probes | `proximityProbeList` |
| Probe registration info | `proximityProbeGetRegistrationInfo` |
| Set registration on a probe | `proximityProbeRegisterSet` |
| Remove a probe registration | `proximityProbeRemove` |
| Name a probe | `proximityProbeSetName` |

### Server & reference

| Action | Command |
|---|---|
| Server info | `serverInfoGet`, `systemInfo` |
| High scores (server / mine) | `getHighScores`, `getMyHighScores` |
| Rift list | `getRifts` |
| Reference data: effects / enums / entity defaults / aggregate fields / definition config / distances | `getEffects`, `getEnums`, `getEntityDefaults`, `getAggregateFields`, `getDefinitionConfigUnits`, `getDistances` |
| Item summary (client reference data) | `getItemSummary` |
| List server commands (client-side reference) | `getCommands` |
| Store: list products / start / finish transaction | `steamGetProducts`, `steamStartTransaction`, `steamFinishTransaction` |
