# MacBook Development Setup

## OSRS Community Bingo Platform

**Status:** Setup checklist v1.0  
**Last updated:** 2026-07-11

## 1. What is required

The development machine needs:

1. Apple command-line developer tools
2. Git
3. An editor or IDE
4. .NET 10 SDK
5. A container runtime with Docker Compose support
6. Access to the project repository

PostgreSQL does not need to be installed directly on macOS. It will run in a local container.

Cloudflare, Hetzner, and production deployment tools are not required for the first coding milestone.

## 2. Check the Mac architecture

Open Terminal and run:

```bash
uname -m
```

Expected result on a new Apple Silicon Mac:

```text
arm64
```

Use Arm64 installers when offered. Do not install x64 versions unless a specific dependency later requires them.

## 3. Apple command-line developer tools

Open Terminal and run:

```bash
xcode-select --install
```

These tools provide Git and common build utilities. A full Xcode installation is not required for this web project.

After installation, verify:

```bash
git --version
```

## 4. Editor recommendation

### Recommended: Visual Studio Code

Install Visual Studio Code for macOS, choosing the Apple Silicon build when offered.

Install these extensions:

- C# Dev Kit by Microsoft
- C# by Microsoft, normally installed as a dependency
- Docker or Container Tools extension by Microsoft, optional but helpful
- EditorConfig support if not already built in

Visual Studio for Mac must not be installed; it is retired and unsupported.

### Alternative: JetBrains Rider

Rider is a strong full C# IDE and works on macOS, but requires a paid license after any trial period. Use Rider instead of VS Code only if preferred; both developers should not be required to install both.

## 5. .NET 10 SDK

Install the .NET 10 SDK for macOS Arm64 from Microsoft's official download page.

The SDK includes the ASP.NET Core runtime needed for local development.

Verify in Terminal:

```bash
dotnet --info
dotnet --version
```

The output should report:

- A 10.x SDK
- `arm64` architecture on Apple Silicon
- macOS as the operating system

Do not install only the runtime. Development requires the SDK.

## 6. Container runtime

### Recommended starting choice: Docker Desktop

Install Docker Desktop for Mac with Apple Silicon support.

Docker Desktop provides:

- Docker Engine compatibility
- Docker Compose
- Container logs and controls
- A straightforward first-time Mac setup

Verify:

```bash
docker --version
docker compose version
docker run --rm hello-world
```

Docker Desktop may request permission for networking, virtualization, or privileged helper installation. These are normal for its container runtime.

### Possible later alternative: OrbStack

OrbStack can replace Docker Desktop and is popular for macOS development, but it is not necessary. Start with one container runtime, not both.

## 7. Git identity

Configure the name and email used for commits:

```bash
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
```

Check the configuration:

```bash
git config --global --list
```

The commit email may be a GitHub-provided private email if preferred.

## 8. GitHub access

Required eventually:

- GitHub account
- Access to the project repository
- Authentication for `git push`

Recommended authentication choices:

- SSH key stored in the macOS keychain, or
- GitHub CLI authentication

GitHub CLI is helpful but not required on day one.

## 9. Optional package manager

Homebrew is optional. It is useful for installing command-line utilities but is not required if VS Code, .NET, and Docker Desktop are installed from their official installers.

Avoid installing tools both through Homebrew and standalone installers unless there is a reason; duplicate .NET or Docker installations can make paths confusing.

## 10. Optional database tools

PostgreSQL will run through Docker Compose. No native PostgreSQL server is required.

Optional graphical database clients:

- DBeaver Community
- pgAdmin
- JetBrains database tools when using Rider

The application and migrations remain the authoritative way to change schema. A graphical client is for inspection and troubleshooting, not unmanaged production edits.

## 11. Optional HTTP and API tools

Not required initially:

- Bruno
- Postman
- Insomnia

Most workflows are browser-based and automated integration tests will cover server endpoints.

## 12. Browser requirements

Keep at least:

- Safari, for native macOS/mobile-WebKit behavior
- A Chromium browser such as Chrome or Edge, for primary development tools and compatibility testing

Firefox is optional but useful before release.

## 13. Secrets and passwords

Use a password manager for:

- Admin bootstrap credentials
- Cloudflare/R2 credentials
- Hetzner credentials
- Production environment secrets
- Backup encryption secret

Never store production credentials in:

- Git
- Markdown files
- Shell history
- Screenshots
- `.env` files committed to the repository

The repository will provide example configuration containing names but no real secret values.

## 14. Tools needed later, not now

These are added only when their milestone begins:

- Cloudflare account and R2 bucket: evidence-upload milestone
- Hetzner account and VPS: production-operations milestone
- Domain and DNS configuration: production-operations milestone
- GitHub Container Registry deployment credentials: deployment milestone
- External uptime monitor: production-operations milestone

There is no reason to start paying for a VPS during the first local milestones.

## 15. Final verification checklist

Before Milestone 1 coding begins, these commands should succeed:

```bash
uname -m
git --version
dotnet --info
docker --version
docker compose version
docker run --rm hello-world
```

Also verify:

- VS Code or Rider opens successfully.
- C# language support loads.
- Terminal is available inside the editor.
- Git name and email are configured.
- There is sufficient free disk space for Docker images and PostgreSQL development data.

## 16. Recommended minimal installation order

1. Apple command-line developer tools
2. Visual Studio Code
3. C# Dev Kit
4. .NET 10 Arm64 SDK
5. Docker Desktop for Apple Silicon
6. Git identity and repository access

Stop there initially. Optional database clients, package managers, API tools, and production accounts can be added when they become useful.

## 17. Official references

- [.NET installation on macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
- [.NET downloads](https://dotnet.microsoft.com/download)
- [Visual Studio for Mac retirement](https://learn.microsoft.com/en-us/lifecycle/announcements/visual-studio-mac-end-of-servicing)
- [Visual Studio Code](https://code.visualstudio.com/)
- [Docker Desktop for Mac](https://docs.docker.com/desktop/setup/install/mac-install/)
- [GitHub SSH setup](https://docs.github.com/en/authentication/connecting-to-github-with-ssh)

