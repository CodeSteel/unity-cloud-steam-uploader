# Unity Cloud Build to Steam Uploader

Fully-automated Unity Cloud Build to Steam uploader, for a complete CI/CD. By using a Post Export function, we can start our own steamcmd process, authenticate it using the config.vdf method, & upload the build directory to Steam. All without any extra services.

## Dependencies

https://github.com/Huntrox/UnityDiscordWebhooks - You can easily remove/replace this if you don't want it...

## Setup

1. Clone this repository to the root if your unity project.

2. Download the latest [Steamworks CLI](https://partner.steamgames.com/doc/sdk)

3. Place the executable in a temp folder, run it with `./steamcmd.exe +login <username> <password> +quit`, then type in the steam guard code when it asks you. Once it's done, copy the contents from `./config/config.vdf`, encode it to [Base64](https://www.base64encode.org/), copy the result and save it for now.

4. Now you can take the steamcmd.exe out of the temp folder, place it next to the UCBSteamUploader script at `Assets/Editor/Builder/`, and delete the temp folder and everything in it as we don't need it anymore.

5. Now create a Unity build configuration.

<img width="1434" height="676" alt="image" src="https://github.com/user-attachments/assets/4c0a26fe-4344-46ed-8662-660ac9e00539" />

6. In Advanced Settings -> Script Hooks, set the Post Export function to `Game.Builder.UCBSteamUploader.PostExport` 

<img width="1380" height="564" alt="image" src="https://github.com/user-attachments/assets/d1af2485-aaa8-451b-beaf-b17b84b6fa40" />

7. In Advanced Settings -> Environment variables, setup using these variables.

`STEAM_USER` - Steam account username.
`STEAM_CONFIG` - Paste the base64 result from step #3.
`DISCORD_WEBHOOK` - The discord webhook URL.

<img width="1396" height="345" alt="image" src="https://github.com/user-attachments/assets/7b81d67a-9697-43cb-af0b-0be28171f460" />

8. Lastly just edit the Builds dictionary in the UCBSteamUploader.cs script to configure your build depot targets. The string key is the buildTargetId which you can find by editing your build configuration and looking at the URL `https://cloud.unity.com/home/organizations/ORG_ID/projects/PROJECT_ID/cloud-build/setup/buildTarget/BUILD_TARGET_WILL_BE_HERE`, the value is the App Id and the target Depot Id you want that build to upload to.
