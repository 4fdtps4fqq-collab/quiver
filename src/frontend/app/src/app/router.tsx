import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "../components/AppShell";
import { ProtectedRoute } from "../components/ProtectedRoute";
import { StudentPortalShell } from "../components/StudentPortalShell";
import { useSession } from "../auth/SessionContext";
import { resolveHomePath } from "../lib/home-path";
import { LoginPage } from "../pages/LoginPage";
import { DashboardPage } from "../pages/DashboardPage";
import { StudentsPage } from "../pages/StudentsPage";
import { CoursesPage } from "../pages/CoursesPage";
import { EnrollmentsPage } from "../pages/EnrollmentsPage";
import { LessonsPage } from "../pages/LessonsPage";
import { EquipmentPage } from "../pages/EquipmentPage";
import { MaintenancePage } from "../pages/MaintenancePage";
import { FinancePage } from "../pages/FinancePage";
import { SchoolSettingsPage } from "../pages/SchoolSettingsPage";
import { SchoolCollaboratorsPage } from "../pages/SchoolCollaboratorsPage";
import { SchoolInstructorSchedulePage } from "../pages/SchoolInstructorSchedulePage";
import { SchoolInvitationsPage } from "../pages/SchoolInvitationsPage";
import { SchoolAuditPage } from "../pages/SchoolAuditPage";
import { StudentPortalPage } from "../pages/StudentPortalPage";
import { StudentPortalHistoryPage } from "../pages/StudentPortalHistoryPage";
import { StudentPortalNotificationsPage } from "../pages/StudentPortalNotificationsPage";
import { StudentPortalProfilePage } from "../pages/StudentPortalProfilePage";
import { SystemSchoolsPage } from "../pages/SystemSchoolsPage";
import { SystemSchoolsDirectoryPage } from "../pages/SystemSchoolsDirectoryPage";
import { platformPermissions } from "../lib/permissions";

export function AppRouter() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <ProtectedRoute allowedRoles={["SystemAdmin", "Owner", "Admin", "Instructor"]}>
            <AppShell />
          </ProtectedRoute>
        }
      >
        <Route index element={<RoleHomeRedirect />} />
        <Route
          path="/system/schools"
          element={
            <ProtectedRoute allowedRoles={["SystemAdmin"]}>
              <SystemSchoolsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/system/schools/directory"
          element={
            <ProtectedRoute allowedRoles={["SystemAdmin"]}>
              <SystemSchoolsDirectoryPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.dashboardView]}
            >
              <DashboardPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/students"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.studentsManage]}
            >
              <StudentsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/instructors"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.instructorsManage]}
            >
              <Navigate to="/school/instructors/schedule" replace />
            </ProtectedRoute>
          }
        />
        <Route
          path="/courses"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.coursesManage]}
            >
              <CoursesPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/enrollments"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.enrollmentsManage]}
            >
              <EnrollmentsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/lessons"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.lessonsManage]}
            >
              <LessonsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/equipment"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.equipmentManage]}
            >
              <EquipmentPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/maintenance"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.maintenanceManage]}
            >
              <MaintenancePage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/finance"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.financeManage]}
            >
              <FinancePage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/school"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.schoolManage]}
            >
              <SchoolSettingsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/school/collaborators"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.schoolManage]}
            >
              <SchoolCollaboratorsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/school/instructors/schedule"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.schoolManage]}
            >
              <SchoolInstructorSchedulePage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/school/invitations"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.schoolManage]}
            >
              <SchoolInvitationsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/school/audit"
          element={
            <ProtectedRoute
              allowedRoles={["Owner", "Admin", "Instructor"]}
              requiredPermissions={[platformPermissions.schoolManage]}
            >
              <SchoolAuditPage />
            </ProtectedRoute>
          }
        />
      </Route>
      <Route
        element={
          <ProtectedRoute allowedRoles={["Student"]}>
            <StudentPortalShell />
          </ProtectedRoute>
        }
      >
        <Route path="/student" element={<StudentPortalPage />} />
        <Route path="/student/history" element={<StudentPortalHistoryPage />} />
        <Route path="/student/notifications" element={<StudentPortalNotificationsPage />} />
        <Route path="/student/profile" element={<StudentPortalProfilePage />} />
      </Route>
      <Route path="*" element={<RoleHomeRedirect />} />
    </Routes>
  );
}

function RoleHomeRedirect() {
  const { user } = useSession();
  return <Navigate to={resolveHomePath(user)} replace />;
}
