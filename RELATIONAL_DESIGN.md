# Relational Design

## Entity-Relationship Diagram

```mermaid
erDiagram
    PATIENT ||--o{ BOOKING : "places (1:N)"
    BOOKING ||--|{ BOOKING_ITEM : "contains (1:N)"
    DIAGNOSTIC_SERVICE ||--o{ BOOKING_ITEM : "included in (1:N)"

    PATIENT {
        int Id PK
        string FullName
        datetime DateOfBirth
        string ContactNumber
        datetime CreatedAt
    }

    DIAGNOSTIC_SERVICE {
        int Id PK
        string Code
        string Name
        string Description
        int DailyCapacity
    }

    BOOKING {
        int Id PK
        string ReferenceNumber
        int PatientId FK
        datetime BookingDate
        datetime CreatedAt
        string Status
    }

    BOOKING_ITEM {
        int Id PK
        int BookingId FK
        int DiagnosticServiceId FK
        datetime CreatedAt
    }
```

## Tables and Relationships

| Entity | Primary Key | Foreign Keys / Constraints | Description |
| :--- | :--- | :--- | :--- |
| **Patient** | `Id` (Auto) | None | Existing patient registry (fictional seed data). |
| **DiagnosticService** | `Id` (Auto) | `Code` (Unique), `DailyCapacity > 0` | Services offered with fixed daily booking capacity limits. |
| **Booking** | `Id` (Auto) | `PatientId` -> `Patient.Id`, `ReferenceNumber` (Unique) | Booking header for a patient on a specific date. |
| **BookingItem** | `Id` (Auto) | `BookingId` -> `Booking.Id` (Cascade), `DiagnosticServiceId` -> `DiagnosticService.Id` (Restrict) | Junction line item; composite index `(BookingId, DiagnosticServiceId)` prevents duplicate services. |
