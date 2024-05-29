# setup-gitflow.ps1

Write-Output "Starting Dev Environment..."

# Check if Docker is installed
Write-Output "Checking for Docker installation..."
if (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Output "Docker is installed."
} else {
    Write-Output "Error: Docker is not installed. Please install Docker and try again."
    exit 1
}

# Check if .NET 8 is installed
Write-Output "Checking for .NET 8 installation..."
$dotnetVersion = dotnet --version
if ($dotnetVersion -match "^8\..*") {
    Write-Output ".NET 8 is installed."
} else {
    Write-Output "Error: .NET 8 is not installed. Please install .NET 8 and try again."
    exit 1
}

# Determine the path to the .gitflow-config file
$gitConfigPath = Join-Path -Path (Get-Location) -ChildPath ".\.gitflow-config"

# Check if the .gitflow-config file exists
Write-Output "Checking for .gitflow-config file..."
if (Test-Path $gitConfigPath) {
    Write-Output "Found .gitflow-config at $gitConfigPath"
    Write-Output "Configuring Git Flow settings..."
    git config --local include.path $gitConfigPath

    # Verify that the configuration was applied
    $result = git config --local --get include.path
    if ($result -eq $gitConfigPath) {
        Write-Output "Git Flow configuration successfully applied."
    } else {
        Write-Output "Failed to apply Git Flow configuration."
    }
} else {
    Write-Output "Error: .gitflow-config file not found at $gitConfigPath"
    Write-Output "Please ensure the .gitflow-config file exists in the parent directory."
}

Write-Output "Git Flow setup completed."
