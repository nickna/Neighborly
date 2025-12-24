# Contributing to Neighborly

## Welcome Contributors! 🙌

We're thrilled that you're considering contributing to Neighborly! Your contributions, whether big or small, help make this vector database better for everyone in the community.

## How You Can Contribute

### 🐛 Report Bugs

Found a bug? We'd love to know about it! Here's how to report it effectively:

**Before Reporting:**
- Check existing issues to avoid duplicates
- Verify the bug exists in the latest version
- Test on a minimal reproduction case

**Bug Report Template:**
```markdown
## Bug Description
Brief description of the issue

## Steps to Reproduce
1. Step one
2. Step two
3. Step three

## Expected Behavior
What should happen

## Actual Behavior
What actually happens

## Environment
- Neighborly Version: 
- .NET Version: 
- Operating System: 
- Architecture (x64/ARM): 

## Additional Context
Any other relevant information, logs, or screenshots
```

### 💡 Suggest Features

Have an idea for improvement? Share it with us!

**Feature Request Template:**
```markdown
## Feature Summary
Brief description of the proposed feature

## Motivation
Why is this feature needed? What problem does it solve?

## Detailed Description
Detailed explanation of the feature

## Alternatives Considered
Other approaches you've considered

## Additional Context
Any other relevant information
```

### 🚀 Submit Code Changes

Ready to contribute code? Here's our process:

#### Development Setup

1. **Prerequisites:**
   ```bash
   # Install .NET 8 SDK
   dotnet --version  # Should be 8.0 or higher
   
   # Install Git
   git --version
   
   # Install Docker (for testing)
   docker --version
   ```

2. **Fork and Clone:**
   ```bash
   # Fork the repository on GitHub
   git clone https://github.com/yourusername/neighborly.git
   cd neighborly
   ```

3. **Setup Development Environment:**
   ```bash
   # Run setup script
   ./setup-dev.ps1  # Windows PowerShell
   # or
   chmod +x setup-dev.sh && ./setup-dev.sh  # Linux/macOS
   
   # Restore dependencies
   dotnet restore
   
   # Build project
   dotnet build --configuration Debug
   ```

4. **Run Tests:**
   ```bash
   # Run all tests
   dotnet test --configuration Debug --verbosity normal
   
   # Run specific test project
   dotnet test Tests/Tests.csproj --configuration Debug
   ```

#### Development Workflow

1. **Create Feature Branch:**
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/your-bug-fix
   ```

2. **Make Changes:**
   - Follow coding conventions (see below)
   - Add tests for new functionality
   - Update documentation as needed
   - Keep commits focused and atomic

3. **Test Changes:**
   ```bash
   # Run full test suite
   dotnet test
   
   # Run specific tests
   dotnet test --filter "TestCategory=YourCategory"
   
   # Check code coverage (if tools available)
   dotnet test --collect:"XPlat Code Coverage"
   ```

4. **Commit Changes:**
   ```bash
   # Stage changes
   git add .
   
   # Commit with descriptive message
   git commit -m "Add feature: brief description
   
   Detailed explanation of what this commit does,
   why it's needed, and any breaking changes."
   ```

5. **Push and Create PR:**
   ```bash
   git push origin feature/your-feature-name
   ```
   Then create a Pull Request on GitHub using our template.

## Coding Standards

### C# Conventions

Follow the [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions):

```csharp
// Use PascalCase for public members
public class VectorDatabase
{
    // Use camelCase for private fields with underscore prefix
    private readonly ReaderWriterLockSlim _rwLock;
    
    // Use PascalCase for properties
    public int Count { get; private set; }
    
    // Use camelCase for parameters and local variables
    public async Task AddAsync(Vector vector, CancellationToken cancellationToken = default)
    {
        var vectorId = vector.Id;
        // Implementation
    }
}

// Use meaningful names
public class EuclideanDistanceCalculator : IDistanceCalculator
{
    // Clear, descriptive method names
    public float CalculateDistance(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        // Implementation
    }
}
```

### File Organization

```
Neighborly/
├── Core/                    # Core abstractions and interfaces
├── Distance/               # Distance calculation implementations
├── ETL/                    # Import/export functionality
├── Search/                 # Search algorithm implementations
└── Storage/                # Storage and persistence
```

### Documentation

- Use XML documentation for public APIs:
  ```csharp
  /// <summary>
  /// Calculates the Euclidean distance between two vectors.
  /// </summary>
  /// <param name="vector1">The first vector.</param>
  /// <param name="vector2">The second vector.</param>
  /// <returns>The Euclidean distance as a float.</returns>
  public float CalculateDistance(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
  ```

- Add inline comments for complex logic:
  ```csharp
  // Use the triangle inequality to prune search space
  if (Math.Abs(queryDistance - nodeDistance) > radius)
  {
      return; // Skip this subtree
  }
  ```

### Testing Guidelines

#### Test Structure
```csharp
[TestFixture]
public class VectorDatabaseTests
{
    private VectorDatabase _database;
    
    [SetUp]
    public void Setup()
    {
        _database = new VectorDatabase();
    }
    
    [TearDown]
    public void TearDown()
    {
        _database?.Dispose();
    }
    
    [Test]
    public async Task AddAsync_WithValidVector_ShouldIncreaseCount()
    {
        // Arrange
        var vector = new Vector(new float[] { 1.0f, 2.0f, 3.0f });
        var initialCount = _database.Count;
        
        // Act
        await _database.AddAsync(vector);
        
        // Assert
        Assert.That(_database.Count, Is.EqualTo(initialCount + 1));
    }
}
```

#### Test Categories
Use test categories to organize tests:
```csharp
[Test, Category("Unit")]
public void CalculateDistance_ShouldReturnCorrectValue() { }

[Test, Category("Integration")]
public async Task ImportData_ShouldLoadAllVectors() { }

[Test, Category("Performance")]
public async Task SearchAsync_WithLargeDataset_ShouldCompleteQuickly() { }
```

#### Test Data
- Use deterministic test data when possible
- Create helper methods for common test scenarios:
  ```csharp
  private static Vector CreateTestVector(params float[] values)
  {
      return new Vector(values);
  }
  
  private static IEnumerable<Vector> CreateTestVectors(int count, int dimensions)
  {
      var random = new Random(42); // Fixed seed for reproducibility
      for (int i = 0; i < count; i++)
      {
          var values = new float[dimensions];
          for (int j = 0; j < dimensions; j++)
          {
              values[j] = (float)random.NextDouble();
          }
          yield return new Vector(values);
      }
  }
  ```

## Pull Request Process

### PR Template
When creating a pull request, use this template:

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Performance improvement
- [ ] Code refactoring

## Testing
- [ ] Tests pass locally
- [ ] New tests added for new functionality
- [ ] Integration tests updated if needed
- [ ] Performance benchmarks run (if applicable)

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Code is commented, particularly in hard-to-understand areas
- [ ] Documentation updated
- [ ] No new warnings introduced
- [ ] Breaking changes documented

## Screenshots (if applicable)

## Additional Notes
```

### PR Review Process

1. **Automated Checks:**
   - CI/CD pipeline runs all tests
   - Code quality checks (if configured)
   - Security scans

2. **Code Review:**
   - At least one maintainer review required
   - Address all feedback before merging
   - Squash commits if requested

3. **Merge:**
   - Use "Squash and merge" for feature branches
   - Use "Merge commit" for important milestone merges

## Commit Message Guidelines

Follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only changes
- `style`: Changes that don't affect code meaning
- `refactor`: Code change that neither fixes a bug nor adds a feature
- `perf`: Performance improvement
- `test`: Adding missing tests or correcting existing tests
- `chore`: Changes to build process or auxiliary tools

**Examples:**
```
feat(search): add HNSW algorithm implementation

Add Hierarchical Navigable Small World (HNSW) algorithm for 
approximate nearest neighbor search. Improves search performance 
for large datasets while maintaining good recall rates.

Closes #123
```

```
fix(database): resolve race condition in index rebuilding

The background index service was occasionally accessing vectors
during database modifications, causing inconsistent results.
Added proper synchronization using the existing reader-writer lock.

Fixes #456
```

## Documentation Contributions

### Documentation Types

1. **API Documentation:**
   - XML documentation in code
   - Automatically generated reference docs

2. **User Guides:**
   - Getting started tutorials
   - How-to guides for specific scenarios
   - Best practices and recommendations

3. **Developer Documentation:**
   - Architecture explanations
   - Algorithm implementations
   - Extension points

### Documentation Style

- Use clear, concise language
- Include code examples for complex concepts
- Add diagrams where helpful
- Keep examples up-to-date with current API

### Documentation Build
```bash
# Generate API documentation
dotnet tool install -g docfx
docfx build docs/docfx.json

# Preview documentation
docfx serve docs/_site
```

## Community Guidelines

### Code of Conduct

We are committed to providing a welcoming and inclusive experience for everyone. Please read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

### Communication

- **GitHub Issues**: Bug reports, feature requests, discussions
- **GitHub Discussions**: General questions, ideas, showcase
- **Pull Requests**: Code review and collaboration

### Getting Help

- Check existing documentation and issues first
- Ask questions in GitHub Discussions
- Tag maintainers only for urgent issues
- Be patient and respectful

## Recognition

Contributors are recognized in several ways:

- **Contributors file**: All contributors listed in CONTRIBUTORS.md
- **Release notes**: Significant contributions highlighted
- **GitHub**: Contributor badge and statistics
- **Community**: Recognition in discussions and social media

## Development Environment Tips

### Recommended Tools

- **IDE**: Visual Studio 2022, VS Code, or JetBrains Rider
- **Extensions**: 
  - C# Dev Kit (VS Code)
  - NUnit Test Adapter
  - GitLens
  - EditorConfig

### Useful Commands

```bash
# Quick development cycle
dotnet watch test  # Auto-run tests on file changes
dotnet watch run   # Auto-restart on changes

# Code formatting
dotnet format      # Format code according to .editorconfig

# Package management
dotnet outdated    # Check for package updates
dotnet audit       # Security vulnerability check
```

### Performance Testing

```bash
# Run benchmarks
dotnet run --project Benchmarks --configuration Release

# Profile specific scenarios
dotnet run --project ProfileApp --configuration Release
```

## Release Process

For maintainers and significant contributors:

1. **Version Planning**: Discussed in GitHub issues
2. **Feature Freeze**: Stop adding new features
3. **Testing Phase**: Comprehensive testing
4. **Documentation**: Update all documentation
5. **Release**: Tag and publish packages
6. **Announcement**: Community announcement

## Questions?

Don't hesitate to ask! We're here to help:

- Create a [GitHub Discussion](https://github.com/nickna/Neighborly/discussions)
- Comment on relevant issues
- Reach out to maintainers

Thank you for contributing to Neighborly! 🚀