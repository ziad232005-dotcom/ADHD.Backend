# SMS verification setup

The backend sends verification codes by SMS through an Android phone that
runs the open-source **SMS Gateway for Android** app (Local Server mode).
The messages are sent from that phone's SIM card, so they use the SIM's
SMS balance.

## 1. Prepare the phone

1. Install **SMS Gateway for Android** (https://docs.sms-gate.app/) and
   allow the SMS permission.
2. On the Home tab, turn on the **Local Server** card and tap
   **Start Service**.
3. Write down the phone's local IP address, the port (usually 8080), the
   username and the password shown in the **Local Server** section.
4. The phone and the computer that runs this backend must be on the
   **same Wi-Fi network**. Keep the phone charged and the app running.

## 2. Give the backend the phone's details (User Secrets)

Run these commands inside the backend folder. Replace the values.

```
dotnet user-secrets set "SmsGateway:BaseUrl" "http://192.168.1.50:8080"
dotnet user-secrets set "SmsGateway:Username" "YOUR_USERNAME"
dotnet user-secrets set "SmsGateway:Password" "YOUR_PASSWORD"
```

Never put the real password in `appsettings.json` or commit it.

## 3. Test

Start the backend (`dotnet run`), then in PowerShell:

```
Invoke-RestMethod -Method Post -Uri http://localhost:5187/api/auth/send-sms-otp `
  -ContentType "application/json" -Body '{"phone":"01012345678"}'

Invoke-RestMethod -Method Post -Uri http://localhost:5187/api/auth/verify-sms-otp `
  -ContentType "application/json" -Body '{"phone":"01012345678","code":"123456"}'
```

## Rules

- Only Egyptian mobile numbers are accepted (`SmsGateway:AllowedPrefixes`).
- A new code can be requested every 60 seconds; a code lives 5 minutes.
- After 5 wrong attempts the code is discarded.
- Each IP address can request at most 5 codes per minute.
- Codes are kept in memory, so they are lost when the server restarts.
