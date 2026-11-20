# Verification Checklist

Record of verification for all Performance Task 4 acceptance scenarios.

| Scenario ID | Scenario | Expected Result | How Verified | Status |
| :--- | :--- | :--- | :--- | :--- |
| **AC-01** | View diagnostic services for a selected date. | Remaining capacity is correctly calculated from persisted information. | Checked in UI "Daily Service Availability" and verified by test `AC01_RemainingCapacity_IsCorrectlyCalculatedFromPersistedBookings`. | **Verified** |
| **AC-02** | Create a booking containing multiple available services. | One confirmed booking is recorded, including every selected service. | Checked in UI "New Booking" and verified by test `AC02_CreateBooking_WithMultipleAvailableServices_SucceedsAtomically`. | **Verified** |
| **AC-03** | Request a fully booked service or submit invalid booking information. | The request is rejected without creating incomplete or invalid records. | Checked in UI error feedback and verified by test `AC03_RequestFullyBookedService_RejectsEntireBookingWithoutPartialRecords`. | **Verified** |
| **AC-04** | Attempt a booking after previously displayed capacity becomes unavailable. | Availability is revalidated, and the system prevents an overbooking. | Controlled test scenario verified by test `AC04_StaleCapacity_RevalidatedInsideTransaction_PreventsOverbooking`. | **Verified** |
| **AC-05** | A required database operation fails before a booking is committed. | No partial booking or incorrect capacity change is persisted. | Transaction boundary and rollback verified by test `AC05_DatabaseFailureBeforeCommit_RollsBackTransactionWithoutPartialPersistence`. | **Verified** |
| **AC-06** | Close and restart the application after confirming a booking. | Previously recorded bookings and their associated services remain available. | SQLite persistence across application restarts verified by test `AC06_ConfirmedBookings_PersistAcrossContextInstances`. | **Verified** |
