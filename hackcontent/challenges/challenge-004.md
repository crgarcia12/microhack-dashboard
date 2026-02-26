# Modernize a .NET Application

## Goal

Modernize the Contoso University .NET Framework application to .NET 9 and deploy it to Azure App Service using GitHub Copilot’s AI-assisted tooling.

## Actions

* Fork `https://github.com/marconsilva/ghcp-app-mod-dotnet-samples/`, clone your fork in Visual Studio 2022, and confirm the ContosoUniversity project builds.
* Use the Visual Studio “Modernize” flow to sign in to GitHub Copilot, select Claude Sonnet 4.5, and run the guided upgrade to .NET 9 until `dotnet-upgrade-report.md` is produced.
* follow the guide bellow or use the more detailed guide in the repository `readme.md` file to perform all the necessary code steps to complete this challenge.

## Success criteria

* ContosoUniversity solution is forked, cloned, and builds locally.
* The application is upgraded from .NET Framework to .NET 9 with a generated upgrade report.
* Mandatory cloud readiness issues, including authentication migration to Microsoft Entra ID, are fully resolved.
* Azure App Service deployment completes successfully and the modernized app runs in Azure.

## Pre-requirements

* **Visual Studio 2022** (17.8 or later) with the following workloads:
  - ASP.NET and web development
  - .NET desktop development
* **GitHub Copilot subscription** (Individual, Business, or Enterprise)
* **GitHub account** with access to public repositories
* **.NET 9 SDK** installed ([Download here](https://dotnet.microsoft.com/download/dotnet/9.0))
* **Docker Desktop** installed and running (for containerization stage)
* **Azure subscription** with Contributor access to a resource group
* **Azure Developer CLI (azd)** installed ([Installation guide](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd))
* **Git** configured with your GitHub credentials
* Basic familiarity with .NET web applications and Visual Studio

## Step by step guide

### Stage 1: Assessment & Planning

1. **Open the Solution**: Launch Visual Studio 2022 and open `ContosoUniversity.sln` from your forked repository
2. **Start Modernization**: Right-click the solution in Solution Explorer → Select **"Modernize"**
3. **Run Assessment**: Choose **"Upgrade to a newer version of .NET"** and let Copilot analyze your application
4. **Review the Plan**: Open `.github/upgrades/dotnet-upgrade-plan.md` to review:
   - Target framework recommendation (.NET 9)
   - Breaking changes and compatibility issues
   - Package update requirements
   - Migration strategy
5. **Customize if Needed**: Ask Copilot to adjust the plan (e.g., "Target .NET 9 instead of .NET 8")

### Stage 2: Framework Upgrade

1. **Start Upgrade**: In Copilot chat, type: `Continue with upgrade to .NET 9`
2. **Monitor Progress**: Watch the progress window showing completed, current, and pending steps
3. **Approve Commands**: Choose "Allow in this Session" for command execution (or review each individually)
4. **Resolve Issues**: Iteratively fix issues marked for "Investigation" by asking Copilot for guidance
5. **Verify Build**: Run `dotnet clean && dotnet build` to ensure the solution compiles
6. **Test the App**: Press F5 and verify core functionality (pages load, database works, navigation functions)

### Stage 3: CVE Check & Security

1. **Run Security Scan**: Ask Copilot: `Perform a CVE check and comprehensive vulnerability assessment`
2. **Review Report**: Check the generated `vulnerability-assessment.md` for:
   - Critical/high severity vulnerabilities
   - Recommended package versions
   - Security best practices
3. **Apply Updates**: Tell Copilot to proceed with updates for vulnerable packages
4. **Verify**: Run `dotnet restore && dotnet build` and test the application

### Stage 4: Unit Testing

1. **Request Test Plan**: Ask Copilot to create a comprehensive test plan covering controllers, services, and data access
2. **Generate Tests**: Copilot will create test projects (e.g., `ContosoUniversity.Tests`) with xUnit tests
3. **Run Tests**: Use Test Explorer or run `dotnet test --logger "console;verbosity=detailed"`
4. **Review Coverage**: Check `test-implementation-summary.md` for pass/fail stats and coverage percentage
5. **Fix Failures**: Work with Copilot to resolve test failures or document acceptable ones with `[Fact(Skip = "reason")]`
6. **Target**: Aim for 70-80% code coverage on critical business logic

### Stage 5: Containerization

1. **Request Dockerfile**: Ask Copilot to create an optimized multi-stage Dockerfile for Azure deployment
2. **Build Image**: Run `docker build -t contoso-university:latest .`
3. **Test Locally**: 
   - Run: `docker run -d -p 8080:8080 --name contoso-app contoso-university:latest`
   - Or use docker-compose: `docker-compose up -d`
4. **Verify**: Open `http://localhost:8080` and test all functionality
5. **Check Logs**: If issues occur, run `docker logs contoso-app` and ask Copilot for help
6. **Deploy to Azure**: Use the generated `azure.yaml` and Azure Developer CLI (azd) to deploy

### Quick Troubleshooting Tips

- **Build errors**: Copy the full error to Copilot chat and ask for diagnosis
- **Runtime issues**: Share stack traces with Copilot for resolution strategies
- **Test failures**: Determine if it's a test issue or app bug - ask Copilot to analyze
- **Docker problems**: Check container logs and verify environment variables/connection strings

## Learning resources

* https://learn.microsoft.com/visualstudio/ide/visual-studio-github-copilot-extension
* https://learn.microsoft.com/dotnet/architecture/modernize-with-azure-containers/
* https://learn.microsoft.com/dotnet/core/migration/
* https://learn.microsoft.com/azure/app-service/quickstart-dotnetcore
* https://learn.microsoft.com/azure/active-directory/develop/quickstart-v2-aspnet-core-webapp
