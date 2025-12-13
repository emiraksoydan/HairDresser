-- Performance Index Optimizations for HairDresser
-- Run this script to add performance indexes
-- These indexes will significantly improve query performance

-- ============================================
-- NOTIFICATION TABLE INDEXES
-- ============================================

-- Index for getting unread notifications for a user (BadgeManager.GetCountsAsync)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notification_UserId_IsRead' AND object_id = OBJECT_ID('Notification'))
BEGIN
    CREATE INDEX IX_Notification_UserId_IsRead 
    ON Notification(UserId, IsRead)
    INCLUDE (Id, CreatedAt);
END
GO

-- Index for finding notifications by appointment (NotificationManager.MarkReadByAppointmentIdAsync)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notification_AppointmentId' AND object_id = OBJECT_ID('Notification'))
BEGIN
    CREATE INDEX IX_Notification_AppointmentId 
    ON Notification(AppointmentId)
    INCLUDE (UserId, IsRead);
END
GO

-- Index for ordering notifications by creation date (NotificationManager.GetAllNotify)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notification_CreatedAt' AND object_id = OBJECT_ID('Notification'))
BEGIN
    CREATE INDEX IX_Notification_CreatedAt 
    ON Notification(CreatedAt DESC)
    INCLUDE (UserId, IsRead);
END
GO

-- ============================================
-- CHAT THREAD TABLE INDEXES
-- ============================================

-- Index for finding threads by user (BadgeManager.GetCountsAsync, ChatManager.GetThreadsAsync)
-- This covers the WHERE clause: (CustomerUserId == userId || StoreOwnerUserId == userId || FreeBarberUserId == userId)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatThread_UserId_Combo' AND object_id = OBJECT_ID('ChatThread'))
BEGIN
    CREATE INDEX IX_ChatThread_UserId_Combo 
    ON ChatThread(CustomerUserId, StoreOwnerUserId, FreeBarberUserId)
    INCLUDE (CustomerUnreadCount, StoreUnreadCount, FreeBarberUnreadCount, LastMessageAt, CreatedAt);
END
GO

-- Index for finding thread by appointment (ChatManager.SendMessageAsync, GetMessagesAsync)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatThread_AppointmentId' AND object_id = OBJECT_ID('ChatThread'))
BEGIN
    CREATE INDEX IX_ChatThread_AppointmentId 
    ON ChatThread(AppointmentId)
    INCLUDE (CustomerUserId, StoreOwnerUserId, FreeBarberUserId, CustomerUnreadCount, StoreUnreadCount, FreeBarberUnreadCount);
END
GO

-- Index for ordering threads by last message (ChatManager.GetThreadsAsync)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatThread_LastMessageAt' AND object_id = OBJECT_ID('ChatThread'))
BEGIN
    CREATE INDEX IX_ChatThread_LastMessageAt 
    ON ChatThread(LastMessageAt DESC)
    INCLUDE (AppointmentId, CreatedAt);
END
GO

-- ============================================
-- CHAT MESSAGE TABLE INDEXES
-- ============================================

-- Index for getting messages by thread and ordering by creation date (ChatManager.GetMessagesAsync)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ChatMessage_ThreadId_CreatedAt' AND object_id = OBJECT_ID('ChatMessage'))
BEGIN
    CREATE INDEX IX_ChatMessage_ThreadId_CreatedAt 
    ON ChatMessage(ThreadId, CreatedAt DESC)
    INCLUDE (Id, SenderUserId, Text, IsSystem, AppointmentId);
END
GO

-- ============================================
-- APPOINTMENT TABLE INDEXES
-- ============================================

-- Index for finding appointments by status and date (AppointmentManager queries, background services)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Appointment_Status_Date' AND object_id = OBJECT_ID('Appointment'))
BEGIN
    CREATE INDEX IX_Appointment_Status_Date 
    ON Appointment(Status, AppointmentDate)
    INCLUDE (PendingExpiresAt, ChairId, CustomerUserId, BarberStoreUserId, FreeBarberUserId);
END
GO

-- Index for finding active appointments by user (AppointmentManager.EnforceActiveRules)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Appointment_UserId_Status' AND object_id = OBJECT_ID('Appointment'))
BEGIN
    CREATE INDEX IX_Appointment_UserId_Status 
    ON Appointment(CustomerUserId, BarberStoreUserId, FreeBarberUserId, Status)
    INCLUDE (Id, AppointmentDate, StartTime, EndTime);
END
GO

-- Index for getting appointments by ID (frequently used in various managers)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Appointment_Id' AND object_id = OBJECT_ID('Appointment'))
BEGIN
    CREATE INDEX IX_Appointment_Id 
    ON Appointment(Id)
    INCLUDE (Status, CustomerUserId, BarberStoreUserId, FreeBarberUserId, ChairId, AppointmentDate, StartTime, EndTime);
END
GO

-- ============================================
-- IMAGE TABLE INDEXES
-- ============================================

-- Index for finding images by owner (AppointmentNotifyManager, various managers)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Image_OwnerId_OwnerType' AND object_id = OBJECT_ID('Image'))
BEGIN
    CREATE INDEX IX_Image_OwnerId_OwnerType 
    ON Image(ImageOwnerId, OwnerType)
    INCLUDE (ImageUrl, CreatedAt);
END
GO

-- ============================================
-- CHAIR TABLE INDEXES
-- ============================================

-- Index for finding chair by store (AppointmentManager queries)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BarberStoreChair_StoreId' AND object_id = OBJECT_ID('BarberStoreChair'))
BEGIN
    CREATE INDEX IX_BarberStoreChair_StoreId 
    ON BarberStoreChair(StoreId)
    INCLUDE (Id, Name, ManuelBarberId);
END
GO

-- ============================================
-- VERIFICATION
-- ============================================

-- Check created indexes
SELECT 
    OBJECT_NAME(object_id) AS TableName,
    name AS IndexName,
    type_desc AS IndexType
FROM sys.indexes
WHERE name LIKE 'IX_%'
    AND object_id IN (
        OBJECT_ID('Notification'),
        OBJECT_ID('ChatThread'),
        OBJECT_ID('ChatMessage'),
        OBJECT_ID('Appointment'),
        OBJECT_ID('Image'),
        OBJECT_ID('BarberStoreChair')
    )
ORDER BY OBJECT_NAME(object_id), name;
GO

