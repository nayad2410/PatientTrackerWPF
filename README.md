Reconnect Progress Tracker

A WPF-based mental health assessment and progress monitoring application using LiveCharts for interactive charting and robust data management.

🚀 Features

Interactive Time-Series Charts

Real date spacing via LiveCharts.Wpf.DateTimePoint for accurate time gaps

Hover tooltips showing precise scores

Treatment note overlays mapped to data points

Fixed-date axis ticks ensuring every assessment date is labeled

Data Entry & Validation

Score entry for PHQ-9, GAD-7, BDI-II, PCL-5, Y-BOCS with range checks (0–80)

Duplicate-date detection with option to update existing entries

Responsive layout for small and large windows

Clinical Metrics

BDI-II response rate (≥50% improvement) and remission rate (score <14)

Average improvement and count of eligible patients

Individual patient outcome summary with baseline vs. most recent comparison

Data Export

CSV Export for spreadsheet analysis

High-Resolution PNG Report (300 dpi) capturing full layout

Professional-grade export including chart legend, history table, and branded header

All-Time Remissions Analysis

Dedicated window tracking remission trajectories across patient populations

CRUD Operations

Edit/delete via double-click with confirmation dialogs

Data grid with text wrapping, ellipses for long notes, and alternating row styling

📦 Prerequisites

.NET 6.0 (or later) SDK

Visual Studio 2022 (or VS Code with C# extension)

NuGet Packages:

LiveCharts.Wpf

PdfSharp

Microsoft.Win32.Registry (for SaveFileDialog)

🛠️ Installation & Setup

Clone the Repository

git clone https://github.com/your-username/reconnect-progress-tracker.git
cd reconnect-progress-tracker

Restore NuGet Packages

In Visual Studio: right-click the solution ➔ Restore NuGet Packages

Or via CLI:

dotnet restore

Build & Run

Visual Studio: Press F5 or click Debug ➔ Start Debugging

CLI:

dotnet build
dotnet run --project PatientTrackerWPF/PatientTrackerWPF.csproj

📖 Usage

Select or Add a Patient via the dropdown or by typing a new Patient ID.

Enter Assessment Scores and treatment notes, then click Add Score.

Data points appear on the chart; date axis auto-adjusts to show every label.

Double-click any table row to Edit or Delete an entry.

Click Calculate to view clinical metrics; Export to save a text report.

Click Export to CSV or Export to PNG for full-report exports.

📷 Screenshots

🤝 Contributing

Contributions are welcome! Please follow these steps:

Fork the repository

Create a feature branch (git checkout -b feature/YourFeature)

Commit your changes (git commit -m "Add new feature")

Push to your branch (git push origin feature/YourFeature)

Open a Pull Request

Please ensure all new code is covered by unit tests and adheres to the existing coding style.

📄 License

This project is licensed under the MIT License. See LICENSE for details.

Developed by Nabaa Naeem
