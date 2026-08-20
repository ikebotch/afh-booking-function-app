param(
    [string]$BaseUrl = "http://localhost:7071/api",
    [string]$BookingId = "booking-smoke-001",
    [string]$TransactionRef = "S-smoke-001",
    [string]$ClientEmail = "aaa.aaa@afhgroup.com",
    [string]$AdviserEmail = "adviser1@afhazure.onmicrosoft.com",
    [string]$ManagerEmail = "manager.email@afhgroup.com",
    [string]$ContactCentreEmail = "contact.centre@afhgroup.com",
    [string]$FunctionKey = "",
    [string]$BearerToken = "",
    [switch]$WhatIfOnly
)

$ErrorActionPreference = "Stop"

function New-Recipient {
    param(
        [string]$Type,
        [string]$Email,
        [string]$DisplayName
    )

    return @{
        recipientType = $Type
        displayName = $DisplayName
        email = $Email
        mobileNumber = $null
        pushTarget = $null
        preferredChannels = @("Email")
    }
}

function New-NotificationBody {
    param(
        [string]$NotificationType,
        [string]$TemplateKey,
        [string]$RecipientType,
        [string]$RecipientEmail,
        [string]$DisplayName,
        [string]$ActorType
    )

    $runId = [Guid]::NewGuid().ToString("N")
    $recipient = New-Recipient -Type $RecipientType -Email $RecipientEmail -DisplayName $DisplayName

    return @{
        type = @{
            sourceApplication = "Booking"
            name = $NotificationType
        }
        correlationId = "notification-smoke-$runId"
        actor = @{
            actorType = $ActorType
            sourceApplication = "Booking"
            id = "smoke-test"
            displayName = "Smoke Test"
            email = $null
        }
        recipients = @($recipient)
        data = @{
            eventId = "notification-smoke-$runId"
            bookingId = $BookingId
            holdId = $BookingId
            slotId = "slot-smoke-001"
            adviserId = "adviser1@afhazure.onmicrosoft.com"
            adviserName = "Adviser 1"
            transactionRef = $TransactionRef
            startUtc = "2026-08-24T09:00:00Z"
            endUtc = "2026-08-24T10:00:00Z"
            clientName = "Smoke Client"
            clientEmail = $ClientEmail
            clientPhone = "+447700900123"
            greetingName = "there"
            meetingType = "Pensions and Retirement"
            meetingTopic = "Pensions and Retirement"
            meetingMethod = "Online"
            meetingMode = "online"
            meetingDate = "Mon 24 Aug 2026"
            meetingDateDay = "Mon 24 Aug 2026"
            meetingDateTime = "10:00-11:00 (Europe/London)"
            date = "Mon 24 Aug 2026"
            time = "10:00-11:00 (Europe/London)"
            meetingDuration = "60 minutes"
            meetingStatus = "Smoke Test"
            when = "Mon 24 Aug 2026 10:00 (Europe/London) to Mon 24 Aug 2026 11:00 (Europe/London)"
            whenLine = "Mon 24 Aug 2026 10:00 (Europe/London) to Mon 24 Aug 2026 11:00 (Europe/London)"
            whereLine = "Join link: https://example.test/meeting"
            locationLine = "Online"
            travelLine = "Travel: N/A (remote meeting)"
            joinUrl = "https://example.test/meeting"
            joinMeetingLink = "https://example.test/meeting"
            manageBookingLink = "https://example.test/bookings/$BookingId"
            manageBookingLinks = "Manage your booking: https://example.test/bookings/$BookingId"
            viewBookingUrl = "https://example.test/bookings/$BookingId"
            cancelBookingUrl = "https://example.test/bookings/$BookingId/cancel"
            rescheduleBookingUrl = "https://example.test/bookings/$BookingId/reschedule"
            contactNumber = "0800 000 0000"
            contactUsNumber = "0800 000 0000"
            reasonCode = "SmokeTest"
            reasonDetail = "Notification matrix smoke test"
            note = "Notification matrix smoke test"
            changeType = "reschedule"
            approverName = "Booking Manager"
            outcome = "Approved"
            decision = "Approved"
            providerEventId = "provider-event-smoke-001"
            correctionReason = "Smoke test correction"
            recipientType = $RecipientType
            TemplateKey = $TemplateKey
            TemplateVersion = "v1"
            "TemplateKey:Email" = $TemplateKey
            "TemplateVersion:Email" = "v1"
            IdempotencyKey = "notification-smoke:$TemplateKey:$RecipientType:$runId"
        }
        sourceApplication = "Booking"
        notificationType = $NotificationType
        channels = @("Email")
    }
}

$cases = @(
    @{ Type = "BookingConfirmed"; Template = "booking-confirmed"; Recipient = "Client"; Email = $ClientEmail; Name = "Smoke Client"; Actor = "System" },
    @{ Type = "BookingConfirmed"; Template = "booking-confirmed-adviser"; Recipient = "Adviser"; Email = $AdviserEmail; Name = "Adviser 1"; Actor = "System" },
    @{ Type = "BookingConfirmed"; Template = "booking-confirmed-manager"; Recipient = "Manager"; Email = $ManagerEmail; Name = "Booking Manager"; Actor = "System" },

    @{ Type = "BookingCancelled"; Template = "booking-cancelled"; Recipient = "Client"; Email = $ClientEmail; Name = "Smoke Client"; Actor = "System" },
    @{ Type = "BookingCancelled"; Template = "booking-cancelled-adviser"; Recipient = "Adviser"; Email = $AdviserEmail; Name = "Adviser 1"; Actor = "System" },
    @{ Type = "BookingCancelled"; Template = "booking-cancelled-manager"; Recipient = "Manager"; Email = $ManagerEmail; Name = "Booking Manager"; Actor = "System" },

    @{ Type = "BookingRescheduled"; Template = "booking-rescheduled"; Recipient = "Client"; Email = $ClientEmail; Name = "Smoke Client"; Actor = "System" },
    @{ Type = "BookingRescheduled"; Template = "booking-rescheduled-adviser"; Recipient = "Adviser"; Email = $AdviserEmail; Name = "Adviser 1"; Actor = "System" },
    @{ Type = "BookingRescheduled"; Template = "booking-rescheduled-manager"; Recipient = "Manager"; Email = $ManagerEmail; Name = "Booking Manager"; Actor = "System" },

    @{ Type = "AdviserRequestSubmitted"; Template = "adviser-request-submitted"; Recipient = "Client"; Email = $ClientEmail; Name = "Smoke Client"; Actor = "Adviser" },
    @{ Type = "AdviserRequestSubmitted"; Template = "adviser-request-submitted-adviser"; Recipient = "Adviser"; Email = $AdviserEmail; Name = "Adviser 1"; Actor = "Adviser" },
    @{ Type = "AdviserRequestSubmitted"; Template = "adviser-request-submitted-manager"; Recipient = "Manager"; Email = $ManagerEmail; Name = "Booking Manager"; Actor = "Adviser" },
    @{ Type = "AdviserRequestSubmitted"; Template = "adviser-request-submitted-contact-centre"; Recipient = "ContactCentre"; Email = $ContactCentreEmail; Name = "Contact Centre"; Actor = "Adviser" },

    @{ Type = "AdviserRequestOutcome"; Template = "adviser-request-outcome"; Recipient = "Client"; Email = $ClientEmail; Name = "Smoke Client"; Actor = "Manager" },
    @{ Type = "AdviserRequestOutcome"; Template = "adviser-request-outcome-adviser"; Recipient = "Adviser"; Email = $AdviserEmail; Name = "Adviser 1"; Actor = "Manager" },
    @{ Type = "AdviserRequestOutcome"; Template = "adviser-request-outcome-manager"; Recipient = "Manager"; Email = $ManagerEmail; Name = "Booking Manager"; Actor = "Manager" },
    @{ Type = "AdviserRequestOutcome"; Template = "adviser-request-outcome-contact-centre"; Recipient = "ContactCentre"; Email = $ContactCentreEmail; Name = "Contact Centre"; Actor = "Manager" },

    @{ Type = "CalendarEventCorrected"; Template = "calendar-event-corrected"; Recipient = "Client"; Email = $ClientEmail; Name = "Smoke Client"; Actor = "System" },
    @{ Type = "CalendarEventCorrected"; Template = "calendar-event-corrected-adviser"; Recipient = "Adviser"; Email = $AdviserEmail; Name = "Adviser 1"; Actor = "System" },
    @{ Type = "CalendarEventCorrected"; Template = "calendar-event-corrected-manager"; Recipient = "Manager"; Email = $ManagerEmail; Name = "Booking Manager"; Actor = "System" },

    @{ Type = "CalendarEventCorrectionFailed"; Template = "calendar-event-correction-failed"; Recipient = "Client"; Email = $ClientEmail; Name = "Smoke Client"; Actor = "System" },
    @{ Type = "CalendarEventCorrectionFailed"; Template = "calendar-event-correction-failed-adviser"; Recipient = "Adviser"; Email = $AdviserEmail; Name = "Adviser 1"; Actor = "System" },
    @{ Type = "CalendarEventCorrectionFailed"; Template = "calendar-event-correction-failed-manager"; Recipient = "Manager"; Email = $ManagerEmail; Name = "Booking Manager"; Actor = "System" },
    @{ Type = "CalendarEventCorrectionFailed"; Template = "calendar-event-correction-failed-contact-centre"; Recipient = "ContactCentre"; Email = $ContactCentreEmail; Name = "Contact Centre"; Actor = "System" }
)

$endpoint = "$($BaseUrl.TrimEnd('/'))/v1/notifications/requests"
if (-not [string]::IsNullOrWhiteSpace($FunctionKey)) {
    $endpoint = "$endpoint?code=$([uri]::EscapeDataString($FunctionKey))"
}

$headers = @{
    "Content-Type" = "application/json"
}
if (-not [string]::IsNullOrWhiteSpace($BearerToken)) {
    $headers["Authorization"] = "Bearer $BearerToken"
}

$results = @()
foreach ($case in $cases) {
    $body = New-NotificationBody `
        -NotificationType $case.Type `
        -TemplateKey $case.Template `
        -RecipientType $case.Recipient `
        -RecipientEmail $case.Email `
        -DisplayName $case.Name `
        -ActorType $case.Actor

    $json = $body | ConvertTo-Json -Depth 20

    if ($WhatIfOnly) {
        Write-Host "WHATIF $($case.Type) / $($case.Template) / $($case.Recipient) -> $($case.Email)"
        continue
    }

    try {
        $response = Invoke-RestMethod -Method Post -Uri $endpoint -Headers $headers -Body $json
        $results += [pscustomobject]@{
            Status = "Accepted"
            NotificationType = $case.Type
            TemplateKey = $case.Template
            RecipientType = $case.Recipient
            Email = $case.Email
            NotificationRequestId = $response.notificationRequestId
            CorrelationId = $response.correlationId
        }
        Write-Host "OK $($case.Type) / $($case.Template) / $($case.Recipient) -> $($case.Email)"
    }
    catch {
        $results += [pscustomobject]@{
            Status = "Failed"
            NotificationType = $case.Type
            TemplateKey = $case.Template
            RecipientType = $case.Recipient
            Email = $case.Email
            NotificationRequestId = $null
            CorrelationId = $null
            Error = $_.Exception.Message
        }
        Write-Host "FAIL $($case.Type) / $($case.Template) / $($case.Recipient) -> $($case.Email): $($_.Exception.Message)" -ForegroundColor Red
    }
}

$results | Format-Table -AutoSize
