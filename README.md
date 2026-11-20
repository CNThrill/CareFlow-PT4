# CareFlow - Diagnostic Booking Management

**Course Component:** ITSD81 - Desktop Application Development  
**Unit:** IV - Data-Driven Application Architecture and Transactional Workflows  
**Performance Task:** 4  
**Collaboration / Authors:**
- Marc Randolph H. Arpon
- Jose Gilberto F. Sadicon  

---

## 1. Overview
CareFlow is a desktop application built with **C#**, **.NET 8**, **Avalonia UI (MVVM)**, and **Entity Framework Core (SQLite)**. It manages hospital outpatient diagnostic bookings for existing patients while enforcing daily service capacities and transactional atomicity.

---

## 2. How to Run

### A. Run Database Migrations
Migrations and seed data (patients and diagnostic services) are applied automatically on startup. To run migrations manually:
```bash
dotnet ef database update --project CareFlow/CareFlow.csproj
```

### B. Run the Application
```bash
dotnet run --project CareFlow/CareFlow.csproj
```

### C. Run the Test Suite
Executes all acceptance criteria (AC-01 to AC-06) and business rules (BR-01 to BR-10) tests:
```bash
dotnet test CareFlow.sln
```

---

## 3. Fictional Seed Data
- **Patients**: Juan Dela Cruz, Maria Clara Santos, Jose Rizal, Andres Bonifacio, Gabriela Silang
- **Diagnostic Services**:
  - Complete Blood Count (CBC) - Daily Capacity: 5
  - Urinalysis (URN) - Daily Capacity: 4
  - Chest X-Ray (CXR) - Daily Capacity: 3
  - Electrocardiogram (ECG) - Daily Capacity: 2
  - Lipid Panel (LIP) - Daily Capacity: 4
  - Fasting Blood Sugar (FBS) - Daily Capacity: 5
