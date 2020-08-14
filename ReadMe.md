# Personal Cloud iOS App

This project contains Personal Cloud mobile app for iOS.

## Get Started

### Configure GitHub Authentication for NuGet Package Manager

All packages published by us, in which some of them are referenced by this project, are hosted on GitHub.

Because GitHub does not yet allow anonymous access to package feeds, you need to either:

1. use our “public” NuGet feed on GitHub and authenticate with a valid GitHub account;
2. OR, download the package files to your computer and configure NuGet PM to use a local source.

By default, our projects use GitHub as a NuGet package source. You do not need additional configuration to use the feed but do need to modify your local `NuGet.config` to include credentials for authentication.

#### If you wish to use our package source...

First, locate the local `NuGet.config` on your computer. On Windows, this is `%AppData%\NuGet\NuGet.Config`; on macOS, `dotnet` and `nuget` tooling uses `~/.nuget/NuGet/NuGet.Config`, but Visual Studio for Mac uses `~/.config/NuGet/NuGet.Config`.

Then add the following lines to `NuGet.config` between `<packageSources>` tags. **Note: this step is NOT needed for our projects. It is only required if you wish to use our package in your own projects.**

```xml
        <add key="Personal Cloud" value="https://nuget.pkg.github.com/Personal-Cloud/index.json" />
```

And last, you need to follow [this GitHub documentation](https://docs.github.com/en/packages/using-github-packages-with-your-projects-ecosystem/configuring-dotnet-cli-for-use-with-github-packages#authenticating-to-github-packages) and generate access tokens for NuGet PM. Instead of using the configuration file from the documentation, you should add these lines along with your access token:

```xml
  <packageSourceCredentials>
      <Personal_x0020_Cloud>
          <add key="Username" value="YOUR_USERNAME" />
          <add key="ClearTextPassword" value="YOUR_ACCESS_TOKEN" />
      </Personal_x0020_Cloud>
  </packageSourceCredentials>
```

You configuration file should look similar to this after following these steps:

```xml
<!-- ... -->
<configuration>
    <packageSources>
        <!-- ... -->

        <!-- Optional -->
        <add key="Personal Cloud" value="https://nuget.pkg.github.com/Personal-Cloud/index.json" />
    </packageSources>
    <packageSourceCredentials>
        <!-- Required: Replace with your GitHub account info. -->
        <Personal_x0020_Cloud>
            <add key="Username" value="AwesomeOctopus" />
            <add key="ClearTextPassword" value="1234abcd" />
        </Personal_x0020_Cloud>
  </packageSourceCredentials>
</configuration>
<!-- ... -->
```

#### If you do not have a GitHub account and wish to use local package source...

First pick or create a folder on your hard drive to store NuGet packages, this folder must be accessible by NuGet PM (and Visual Studio).

Then download all packages from [our source](https://github.com/orgs/Personal-Cloud/packages), store them in the folder you picked.

Last, locate the local `NuGet.config` on your computer. On Windows, this is `%AppData%\NuGet\NuGet.Config`; on macOS, `dotnet` and `nuget` tooling uses `~/.nuget/NuGet/NuGet.Config`, but Visual Studio for Mac uses `~/.config/NuGet/NuGet.Config`. Add the following lines between `<packageSources>` tags. This step is required to consume local packages from any projects:

```xml
        <add key="local" value="FULL_PATH_TO_FOLDER" />
```

Your `NuGet.config` should look like this:

```xml
<!-- ... -->
<configuration>
    <packageSources>
        <!-- ... -->

        <!-- Required: Replace with path to the folder on your computer. -->
        <add key="local" value="~/NuGet/Packages" />
    </packageSources>
</configuration>
<!-- ... -->
```
