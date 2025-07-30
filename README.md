# 🏥 Mental Health Patient Tracker

[![.NET](https://img.shields.io/badge/.NET-6.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-blueviolet.svg)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![Entity Framework](https://img.shields.io/badge/ORM-Entity%20Framework%20Core-green.svg)](https://docs.microsoft.com/en-us/ef/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A comprehensive **production-ready** WPF desktop application for clinical mental health assessment tracking, featuring real-time data visualization, role-based security, and professional reporting capabilities.

> **Note:** This is a fully functional medical records management system currently deployed in a clinical environment, sanitized for portfolio demonstration.

## 🎯 **Project Overview**

This application solves the critical problem of manual patient progress tracking in mental health clinical settings. Healthcare providers can now efficiently track, analyze, and report on patient outcomes across multiple standardized assessment tools.

### **Key Business Value**
- **Problem Solved:** Eliminated manual spreadsheet tracking for clinical assessments
- **Users Supported:** Multi-role clinical teams (Doctors, Nurses, Researchers, Administrators)
- **Impact:** Improved patient care through data-driven treatment decisions
- **Scalability:** Designed for 100+ concurrent users in clinical environments

## ✨ **Features**

### **🔐 Security & Access Control**
- **BCrypt password hashing** with adaptive work factors
- **Role-based access control** (Admin, Doctor, Nurse, Researcher)
- **Comprehensive audit logging** for all user actions
- **Session management** with automatic timeout
- **HIPAA-compliant** data handling practices

### **📊 Clinical Assessment Tracking**
- **Multi-assessment support:** PHQ-9, GAD-7, BDI-II, PCL-5, Y-BOCS
- **Real-time data visualization** with LiveCharts integration
- **Clinical outcome metrics** (response rates, remission tracking)
- **Treatment note management** with contextual annotations
- **Progress trend analysis** with statistical calculations

### **📈 Data Visualization**
- **Interactive charts** with multiple assessment series
- **Smart date scaling** for optimal visualization
- **Color-coded assessment types** for quick identification
- **Responsive design** adapting to window size changes
- **Treatment note overlays** on progress charts

### **📄 Professional Reporting**
- **Custom GDI+ report generation** for clinical documentation
- **Print-quality output** with professional formatting
- **Dynamic layouts** adapting to variable data quantities
- **Embedded data visualizations** within reports
- **Export capabilities** (PNG, CSV) with save dialog integration

### **🔄 Data Management**
- **Robust CSV import/export** with data validation
- **Error recovery** and detailed user feedback
- **Backup and restore** functionality
- **Data filtering** and search capabilities
- **Database integrity** with Entity Framework migrations

## 🏗️ **Technical Architecture**

### **Design Patterns**
- **MVVM (Model-View-ViewModel)** for clean separation of concerns
- **Dependency Injection** for loose coupling and testability
- **Repository Pattern** with Entity Framework abstraction
- **Service Layer Architecture** for business logic encapsulation
- **Observer Pattern** for real-time UI updates

### **Technology Stack**
```
Frontend:     WPF (.NET 6) + LiveCharts
Backend:      C# with Entity Framework Core
Database:     SQL Server (Azure SQL Database ready)
Security:     BCrypt + Role-based Authorization
Logging:      Serilog with structured logging
Email:        SMTP with HTML templating
Testing:      Unit tests with dependency injection
```

### **Database Design**
- **Code-first approach** with Entity Framework migrations
- **Normalized schema** with proper relationships
- **Audit fields** on all entities (Created/Updated by/at)
- **Index optimization** for performance
- **Foreign key constraints** for data integrity

## 🚀 **Getting Started**

### **Prerequisites**
- Windows 10/11
- .NET 6.0 Runtime
- SQL Server (LocalDB for development)

### **Installation**
```bash
# Clone the repository
git clone https://github.com/yourusername/mental-health-tracker.git

# Navigate to project directory
cd mental-health-tracker

# Restore dependencies
dotnet restore

# Update database
dotnet ef database update

# Run the application
dotnet run --project PatientTrackerWPF
```

### **Configuration**
Update `appsettings.json` with your database connection:
```json
{
  "ConnectionStrings": {
    "PatientDb": "Server=(localdb)\\mssqllocaldb;Database=PatientTrackerDB;Trusted_Connection=true;"
  }
}
```

## 👥 **User Roles & Permissions**

| Role | Permissions |
|------|-------------|
| **Admin** | Full system access + user management |
| **Doctor** | Patient data management + all clinical features |
| **Nurse** | Patient data entry + basic reporting |
| **Researcher** | Read-only access + data export + analytics |

## 📊 **Assessment Tools Supported**

| Assessment | Range | Purpose |
|------------|-------|---------|
| **PHQ-9** | 0-27 | Depression severity screening |
| **GAD-7** | 0-21 | Generalized anxiety disorder |
| **BDI-II** | 0-63 | Beck Depression Inventory |
| **PCL-5** | 0-80 | PTSD symptom assessment |
| **Y-BOCS** | 0-40 | Obsessive-compulsive symptoms |

## 🎨 **Screenshots**

### Login Interface
![Login Screen](screenshots/login-screen.png)

### Patient Dashboard
<img width="1877" height="959" alt="image" src="https://github.com/user-attachments/assets/3a6b9a69-dc7d-4a43-a276-f41c8c4c5794" />



### Data Visualization
![Charts](screenshots/progress-charts.png)

### Professional Reports
<img width="1200" height="1600" alt="ReconnectClinicalReport_DEMO-002_20250721_1119" src="https://github.com/user-attachments/assets/10bf52ff-3b11-4220-b274-b707d56ef2bf" />

<img width="1880" height="400" alt="unnamed" src="https://github.com/user-attachments/assets/2efb8377-9929-4de3-8fc5-e4df234f492e" />

## 🧪 **Key Technical Implementations**

### **Security Implementation**
```csharp
public class AuthenticationService
{
    public string HashPassword(string password)
    {
        // BCrypt with work factor 12 for security/performance balance
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }
    
    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
```

### **Audit Logging**
```csharp
public class AuditService
{
    public async Task LogActionAsync(string action, string entityId, string details)
    {
        var auditLog = new AuditLog
        {
            UserId = _currentUserService.CurrentUser?.Id,
            Action = action,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow,
            IPAddress = GetClientIPAddress()
        };
        
        await _dbContext.AuditLogs.AddAsync(auditLog);
        await _dbContext.SaveChangesAsync();
    }
}
```

### **Clinical Metrics Calculation**
```csharp
public ClinicalMetrics CalculateBDI2Metrics(Dictionary<string, List<ScoreEntry>> patientData)
{
    var metrics = new ClinicalMetrics();
    
    foreach (var patient in patientData)
    {
        var assessments = patient.Value
            .Where(a => a.BDI2.HasValue)
            .OrderBy(a => a.Date)
            .ToList();
            
        if (assessments.Count >= 2 && assessments.First().BDI2 >= 14)
        {
            var baseline = assessments.First().BDI2.Value;
            var latest = assessments.Last().BDI2.Value;
            var improvement = (baseline - latest) / (double)baseline * 100;
            
            if (improvement >= 50) // Response criteria
                metrics.ResponseCount++;
                
            if (latest < 14) // Remission criteria
                metrics.RemissionCount++;
        }
    }
    
    return metrics;
}
```

## 🔧 **Development Highlights**

### **Challenges Solved**
1. **Real-time Data Synchronization** - Implemented observable collections with automatic UI binding
2. **Complex Business Logic** - Clinical outcome calculations following research standards
3. **Security Requirements** - Healthcare-grade security with comprehensive audit trails
4. **Performance Optimization** - Efficient chart rendering for large datasets
5. **User Experience** - Intuitive interface for clinical workflow integration

### **Technical Decisions**
- **Desktop over Web:** Better offline capability and clinical workflow integration
- **Entity Framework:** Type safety and migration management for evolving schema
- **LiveCharts:** Real-time data visualization with professional appearance
- **Service Architecture:** Separation of concerns and dependency injection for maintainability

## 📈 **Performance Metrics**
- **Startup Time:** < 3 seconds on typical clinical hardware
- **Data Entry Speed:** < 1 second response time for form submissions
- **Chart Rendering:** Real-time updates with 1000+ data points
- **Report Generation:** Professional reports in < 5 seconds
- **Database Queries:** Optimized with proper indexing and eager loading

## 🤝 **Contributing**

This project demonstrates production-level code quality and architectural decisions. Key areas showcased:

- **Clean Architecture** with clear separation of concerns
- **SOLID Principles** implementation throughout codebase
- **Security Best Practices** for healthcare applications
- **Performance Optimization** for real-world usage
- **Professional Documentation** and code comments

## 📝 **License**

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🎯 **About This Project**

This application represents a complete software development lifecycle from requirements gathering through production deployment. It demonstrates:

- **Problem Analysis** and solution design
- **Technical Architecture** decisions and implementation
- **Security Implementation** for sensitive healthcare data
- **User Experience Design** for clinical workflows
- **Production Deployment** and real-world usage
- **Maintenance and Support** considerations

**Total Development Time:** 6 months (part-time)  
**Lines of Code:** ~15,000+ (C#, XAML, SQL)  
**Current Status:** Production deployment in clinical environment  

## 📞 **Contact**

For technical discussions about this project's architecture, implementation decisions, or code quality, please feel free to reach out.

---

*This project showcases production-ready software development skills including architecture design, security implementation, database design, user interface development, and real-world problem solving in a healthcare environment.*
