$ErrorActionPreference = 'Stop'
Set-Location 'c:\Users\filip\Documents\Kiteflow\KiteFlow'

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Object
    )

    $Object | ConvertTo-Json -Depth 10 | Set-Content -Path $Path -Encoding UTF8
}

function Step {
    param([string]$Message)
    Write-Output "STEP: $Message"
}

$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$tempDir = 'temp\smoke-portal-block'
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

try {
    Step 'Login do SystemAdmin'
    $adminPath = Join-Path $tempDir 'admin-login.json'
    Write-JsonFile $adminPath @{ email = 'admin@quiver.local'; password = 'Admin123!' }
    $admin = (curl.exe -s -X POST http://localhost:7001/api/v1/auth/login -H "Content-Type: application/json" --data-binary "@$adminPath") | ConvertFrom-Json
    $adminToken = $admin.token
    if (-not $adminToken) { throw 'Falha no login do SystemAdmin.' }

    $ownerEmail = "owner.block.$stamp@quiver.local"
    $studentEmail = "student.block.$stamp@quiver.local"

    Step 'Criacao da escola'
    $schoolPath = Join-Path $tempDir 'system-school.json'
    Write-JsonFile $schoolPath @{
        legalName = "Escola Bloqueio $stamp LTDA"
        displayName = "Escola Bloqueio $stamp"
        ownerFullName = 'Owner Bloqueio'
        ownerEmail = $ownerEmail
        ownerPassword = 'Owner123!'
        ownerPhone = '(27) 99999-0000'
        slug = "escola-bloqueio-$stamp"
        timezone = 'America/Sao_Paulo'
        currencyCode = 'BRL'
        themePrimary = '#0B3C5D'
        themeAccent = '#2ED4A7'
        bookingLeadTimeMinutes = 60
        cancellationWindowHours = 24
    }
    $school = (curl.exe -s -X POST http://localhost:7000/api/v1/system/schools -H "Authorization: Bearer $adminToken" -H "Content-Type: application/json" --data-binary "@$schoolPath") | ConvertFrom-Json
    $schoolId = [string]$school.schoolId
    if (-not $schoolId) { throw 'Falha ao criar escola.' }

    Step 'Login do owner'
    $ownerLoginPath = Join-Path $tempDir 'owner-login.json'
    Write-JsonFile $ownerLoginPath @{ email = $ownerEmail; password = 'Owner123!' }
    $owner = (curl.exe -s -X POST http://localhost:7001/api/v1/auth/login -H "Content-Type: application/json" --data-binary "@$ownerLoginPath") | ConvertFrom-Json
    $ownerToken = $owner.token
    if (-not $ownerToken) { throw 'Falha no login do owner.' }

    Step 'Criacao do aluno'
    $studentCreatePath = Join-Path $tempDir 'student-create.json'
    Write-JsonFile $studentCreatePath @{
        fullName = 'Aluno Delinquente'
        email = $studentEmail
        phone = '(27) 99999-1111'
        postalCode = '29010-000'
        street = 'Avenida Teste'
        streetNumber = '100'
        addressComplement = 'Sala 1'
        neighborhood = 'Centro'
        city = 'Vitoria'
        state = 'ES'
        birthDate = '2010-01-10'
        medicalNotes = 'Smoke do bloqueio'
        emergencyContactName = 'Contato Apoio'
        emergencyContactPhone = '(27) 99999-2222'
        identityUserId = $null
        firstStandUpAtUtc = $null
    }
    $student = (curl.exe -s -X POST http://localhost:7003/api/v1/students -H "Authorization: Bearer $ownerToken" -H "Content-Type: application/json" --data-binary "@$studentCreatePath") | ConvertFrom-Json
    $studentId = [string]$student.studentId
    if (-not $studentId) { throw 'Falha ao criar aluno.' }

    Step 'Criacao do usuario do aluno'
    $studentUserId = [guid]::NewGuid().ToString()
    $bootstrapPath = Join-Path $tempDir 'student-bootstrap.json'
    Write-JsonFile $bootstrapPath @{
        userId = $studentUserId
        schoolId = $schoolId
        email = $studentEmail
        password = 'Student123!'
        role = 4
    }
    $bootstrap = (curl.exe -s -X POST http://localhost:7001/api/v1/auth/bootstrap-user -H "Content-Type: application/json" --data-binary "@$bootstrapPath") | ConvertFrom-Json
    if (-not $bootstrap.email) { throw 'Falha ao criar usuário do aluno.' }

    Step 'Vinculo do aluno com Identity'
    $studentUpdatePath = Join-Path $tempDir 'student-update.json'
    Write-JsonFile $studentUpdatePath @{
        fullName = 'Aluno Delinquente'
        email = $studentEmail
        phone = '(27) 99999-1111'
        postalCode = '29010-000'
        street = 'Avenida Teste'
        streetNumber = '100'
        addressComplement = 'Sala 1'
        neighborhood = 'Centro'
        city = 'Vitoria'
        state = 'ES'
        birthDate = '2010-01-10'
        medicalNotes = 'Smoke do bloqueio'
        emergencyContactName = 'Contato Apoio'
        emergencyContactPhone = '(27) 99999-2222'
        identityUserId = $studentUserId
        firstStandUpAtUtc = $null
        isActive = $true
    }
    $studentUpdateStatus = curl.exe -s -o NUL -w "%{http_code}" -X PUT "http://localhost:7003/api/v1/students/$studentId" -H "Authorization: Bearer $ownerToken" -H "Content-Type: application/json" --data-binary "@$studentUpdatePath"
    if ($studentUpdateStatus -ne '200') { throw "Falha ao vincular aluno ao Identity. Status: $studentUpdateStatus" }

    Step 'Criacao do instrutor'
    $instructorPath = Join-Path $tempDir 'instructor-create.json'
    Write-JsonFile $instructorPath @{
        fullName = 'Instrutor Bloqueio'
        email = "instrutor.block.$stamp@quiver.local"
        phone = '(27) 99999-3333'
        specialties = 'Wave'
        hourlyRate = 180.0
        identityUserId = $null
    }
    $instructor = (curl.exe -s -X POST http://localhost:7003/api/v1/instructors -H "Authorization: Bearer $ownerToken" -H "Content-Type: application/json" --data-binary "@$instructorPath") | ConvertFrom-Json
    $instructorId = [string]$instructor.instructorId
    if (-not $instructorId) { throw 'Falha ao criar instrutor.' }

    Step 'Criacao do curso'
    $coursePath = Join-Path $tempDir 'course-create.json'
    Write-JsonFile $coursePath @{
        name = 'Curso Portal Bloqueio'
        level = 1
        totalHours = 6
        price = 1500.0
    }
    $course = (curl.exe -s -X POST http://localhost:7003/api/v1/courses -H "Authorization: Bearer $ownerToken" -H "Content-Type: application/json" --data-binary "@$coursePath") | ConvertFrom-Json
    $courseId = [string]$course.courseId
    if (-not $courseId) { throw 'Falha ao criar curso.' }

    Step 'Criacao da matricula'
    $enrollmentPath = Join-Path $tempDir 'enrollment-create.json'
    Write-JsonFile $enrollmentPath @{
        studentId = $studentId
        courseId = $courseId
        startedAtUtc = '2026-03-14T12:00:00Z'
    }
    $enrollment = (curl.exe -s -X POST http://localhost:7003/api/v1/enrollments -H "Authorization: Bearer $ownerToken" -H "Content-Type: application/json" --data-binary "@$enrollmentPath") | ConvertFrom-Json
    $enrollmentId = [string]$enrollment.enrollmentId
    if (-not $enrollmentId) { throw 'Falha ao criar matrícula.' }

    Step 'Criacao da cobranca vencida'
    $receivablePath = Join-Path $tempDir 'receivable-create.json'
    Write-JsonFile $receivablePath @{
        studentId = $studentId
        studentNameSnapshot = 'Aluno Delinquente'
        enrollmentId = $enrollmentId
        amount = 500.0
        dueAtUtc = '2026-03-01T12:00:00Z'
        description = 'Parcela em atraso'
        notes = 'Smoke do bloqueio'
    }
    $receivable = (curl.exe -s -X POST http://localhost:7005/api/v1/finance/receivables -H "Authorization: Bearer $ownerToken" -H "Content-Type: application/json" --data-binary "@$receivablePath") | ConvertFrom-Json
    $receivableId = [string]$receivable.receivableId
    if (-not $receivableId) { throw 'Falha ao criar conta a receber.' }

    Step 'Login do aluno'
    $studentLoginPath = Join-Path $tempDir 'student-login.json'
    Write-JsonFile $studentLoginPath @{ email = $studentEmail; password = 'Student123!' }
    $studentLogin = (curl.exe -s -X POST http://localhost:7001/api/v1/auth/login -H "Content-Type: application/json" --data-binary "@$studentLoginPath") | ConvertFrom-Json
    $studentToken = $studentLogin.token
    if (-not $studentToken) { throw 'Falha no login do aluno.' }

    Step 'Tentativa bloqueada de agendamento'
    $blockedPath = Join-Path $tempDir 'blocked-schedule.json'
    Write-JsonFile $blockedPath @{
        enrollmentId = $enrollmentId
        instructorId = $instructorId
        startAtUtc = '2026-03-20T13:00:00Z'
        durationMinutes = 90
        notes = 'Tentativa bloqueada'
    }
    $blockedBodyPath = Join-Path $tempDir 'blocked-response.json'
    $blockedStatus = curl.exe -s -o $blockedBodyPath -w "%{http_code}" -X POST http://localhost:7000/academics/api/v1/student-portal/lessons/course -H "Authorization: Bearer $studentToken" -H "Content-Type: application/json" --data-binary "@$blockedPath"
    $blockedBody = Get-Content -Path $blockedBodyPath -Raw

    Step 'Registro do pagamento'
    $paymentPath = Join-Path $tempDir 'payment.json'
    Write-JsonFile $paymentPath @{
        amount = 500.0
        paidAtUtc = '2026-03-14T15:00:00Z'
        note = 'Quitação do smoke'
    }
    $payment = (curl.exe -s -X POST "http://localhost:7005/api/v1/finance/receivables/$receivableId/payments" -H "Authorization: Bearer $ownerToken" -H "Content-Type: application/json" --data-binary "@$paymentPath") | ConvertFrom-Json
    if (-not $payment.paymentId) { throw 'Falha ao registrar pagamento.' }

    Step 'Tentativa liberada de agendamento'
    $allowedPath = Join-Path $tempDir 'allowed-schedule.json'
    Write-JsonFile $allowedPath @{
        enrollmentId = $enrollmentId
        instructorId = $instructorId
        startAtUtc = '2026-03-21T13:00:00Z'
        durationMinutes = 90
        notes = 'Tentativa liberada'
    }
    $allowed = (curl.exe -s -X POST http://localhost:7000/academics/api/v1/student-portal/lessons/course -H "Authorization: Bearer $studentToken" -H "Content-Type: application/json" --data-binary "@$allowedPath") | ConvertFrom-Json
    if (-not $allowed.lessonId) { throw 'Falha ao agendar depois da quitação.' }

    [pscustomobject]@{
        schoolId = $schoolId
        studentId = $studentId
        receivableId = $receivableId
        blockStatus = $blockedStatus
        blockBody = $blockedBody
        paymentStatus = $payment.status
        allowedLessonId = $allowed.lessonId
    } | ConvertTo-Json -Depth 6
}
catch {
    Write-Output "ERROR: $($_.Exception.Message)"
    throw
}
