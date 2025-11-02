using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data.Student;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Controllers.Student
{
    /// <summary>
    /// Controller quản lý tính năng tải lên/minh chứng của sinh viên.
    /// </summary>
    [Authorize(Roles = "STUDENT")]
    [Route("student/proofs")]
    public class StudentProofsController : Controller
    {
        private static readonly string[] AllowedContentTypes =
        {
            "application/pdf",
            "image/png",
            "image/jpeg"
        };

        private static readonly string[] AllowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        private readonly StudentProofsRepository _repository;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<StudentProofsController> _logger;

        public StudentProofsController(StudentProofsRepository repository, IWebHostEnvironment environment, ILogger<StudentProofsController> logger)
        {
            _repository = repository;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, int? activityId = null, string? q = null)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Forbid();
            }

            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 50);
            var keyword = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

            var activities = await _repository.GetMyActivitiesAsync(studentId);
            var paged = await _repository.GetMyProofsAsync(studentId, page, pageSize, activityId, keyword);

            var model = new StudentProofsVm
            {
                StudentId = studentId,
                Activities = activities,
                SelectedActivityId = activityId,
                Keyword = keyword,
                Items = paged
            };

            ViewData["Title"] = "Minh chứng";
            return View(model);
        }

        [HttpGet("upload")]
        public async Task<IActionResult> UploadModal(int? activityId)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var activities = await _repository.GetMyActivitiesAsync(studentId);
            int? selected = null;
            if (activityId.HasValue)
            {
                var reg = await _repository.GetRegistrationAsync(studentId, activityId.Value);
                if (reg != null)
                {
                    selected = activityId.Value;
                }
            }

            var vm = new ProofUploadModalVm
            {
                Activities = activities,
                SelectedActivityId = selected
            };

            return PartialView("_UploadModal", vm);
        }

        [HttpPost("upload")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile? file, int activityId, string? note)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { ok = false, message = "Vui lòng chọn tệp hợp lệ." });
            }

            if (file.Length > MaxFileSize)
            {
                return BadRequest(new { ok = false, message = "Tệp vượt quá giới hạn 5MB." });
            }

            var originalName = SanitizeFileName(file.FileName);
            if (string.IsNullOrEmpty(originalName))
            {
                return BadRequest(new { ok = false, message = "Tên tệp không hợp lệ." });
            }

            var ext = Path.GetExtension(originalName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                return BadRequest(new { ok = false, message = "Chỉ chấp nhận tệp PDF hoặc hình ảnh (PNG/JPG)." });
            }

            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            if (!AllowedContentTypes.Contains(contentType.ToLowerInvariant()))
            {
                return BadRequest(new { ok = false, message = "Định dạng tệp không được hỗ trợ." });
            }
            contentType = contentType.ToLowerInvariant();

            var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (trimmedNote != null && trimmedNote.Length > 300)
            {
                return BadRequest(new { ok = false, message = "Ghi chú tối đa 300 ký tự." });
            }

            var registration = await _repository.GetRegistrationAsync(studentId, activityId);
            if (registration == null)
            {
                return BadRequest(new { ok = false, message = "Bạn chưa đăng ký hoạt động này." });
            }

            string shaHex;
            using (var stream = file.OpenReadStream())
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                shaHex = BitConverter.ToString(hash).Replace("-", string.Empty);
            }

            if (!string.IsNullOrEmpty(shaHex))
            {
                var duplicated = await _repository.HasDuplicateHashAsync(studentId, activityId, shaHex);
                if (duplicated)
                {
                    return BadRequest(new { ok = false, message = "Bạn đã gửi tệp tương tự cho hoạt động này." });
                }
            }

            var uploadsRoot = Path.Combine(_environment.WebRootPath ?? string.Empty, "uploads", "proofs", studentId, activityId.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(uploadsRoot);

            var storedFileName = $"{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(uploadsRoot, storedFileName);
            var relativePath = $"/uploads/proofs/{studentId}/{activityId}/{storedFileName}";

            try
            {
                await using (var fs = System.IO.File.Create(physicalPath))
                {
                    await file.CopyToAsync(fs);
                }

                var dto = new NewProofDto
                {
                    ActivityId = activityId,
                    RegistrationId = registration.RegistrationId,
                    StudentId = studentId,
                    FileName = originalName,
                    StoredPath = relativePath,
                    ContentType = contentType,
                    FileSize = file.Length,
                    Sha256Hex = shaHex,
                    Note = trimmedNote
                };

                var newId = await _repository.CreateAsync(dto);

                await _repository.WriteAuditAsync(studentId, "PROOF_UPLOAD", GetClientIp(), GetUserAgent(), new
                {
                    aid = activityId,
                    proofId = newId,
                    fn = originalName,
                    size = file.Length
                });

                _logger.LogInformation("Sinh viên {Student} tải lên minh chứng {ProofId} cho hoạt động {Activity}", studentId, newId, activityId);
                return Json(new { ok = true, message = "Tải lên thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải lên minh chứng cho sinh viên {StudentId} - hoạt động {ActivityId}", studentId, activityId);
                if (System.IO.File.Exists(physicalPath))
                {
                    try
                    {
                        System.IO.File.Delete(physicalPath);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "Không thể xóa tệp sau khi lỗi tải lên: {Path}", physicalPath);
                    }
                }
                return StatusCode(StatusCodes.Status500InternalServerError, new { ok = false, message = "Không thể lưu minh chứng. Vui lòng thử lại." });
            }
        }

        [HttpPost("delete/{id:long}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var proof = await _repository.GetProofAsync(id, studentId);
            if (proof == null)
            {
                return NotFound();
            }

            if (!string.Equals(proof.Status, "SUBMITTED", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { ok = false, message = "Chỉ có thể xóa minh chứng đang chờ duyệt." });
            }

            var physicalPath = ResolvePhysicalPath(proof.StoredPath);

            try
            {
                var deleted = await _repository.DeleteAsync(id, studentId);
                if (!deleted)
                {
                    return BadRequest(new { ok = false, message = "Không thể xóa minh chứng. Vui lòng thử lại." });
                }

                if (!string.IsNullOrEmpty(physicalPath) && System.IO.File.Exists(physicalPath))
                {
                    try
                    {
                        System.IO.File.Delete(physicalPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Không thể xóa tệp minh chứng trên ổ đĩa: {Path}", physicalPath);
                    }
                }

                await _repository.WriteAuditAsync(studentId, "PROOF_DELETE", GetClientIp(), GetUserAgent(), new
                {
                    proofId = id,
                    aid = proof.ActivityId,
                    fn = proof.FileName
                });

                _logger.LogInformation("Sinh viên {Student} đã xóa minh chứng {ProofId}", studentId, id);
                return Json(new { ok = true, message = "Đã xóa minh chứng." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể xóa minh chứng {ProofId} cho sinh viên {StudentId}", id, studentId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { ok = false, message = "Không thể xóa minh chứng." });
            }
        }

        [HttpGet("download/{id:long}")]
        public async Task<IActionResult> Download(long id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var proof = await _repository.GetProofAsync(id, studentId);
            if (proof == null)
            {
                return NotFound();
            }

            var physicalPath = ResolvePhysicalPath(proof.StoredPath);
            if (string.IsNullOrEmpty(physicalPath) || !System.IO.File.Exists(physicalPath))
            {
                _logger.LogWarning("Không tìm thấy tệp minh chứng {ProofId} trên hệ thống: {Path}", id, physicalPath);
                return NotFound();
            }

            return PhysicalFile(physicalPath, proof.ContentType, proof.FileName);
        }

        private string? GetStudentId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private string GetClientIp()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
        }

        private string GetUserAgent()
        {
            var ua = Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(ua) ? "unknown" : ua;
        }

        /// <summary>
        /// Chuẩn hóa tên tệp để tránh ký tự lạ.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName ?? string.Empty);
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var invalids = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                builder.Append(invalids.Contains(ch) ? '_' : ch);
            }

            var sanitized = builder.ToString().Trim();
            if (sanitized.Length > 255)
            {
                sanitized = sanitized.Substring(0, 255);
            }

            return sanitized;
        }

        /// <summary>
        /// Chuyển đường dẫn lưu trong DB thành đường dẫn vật lý.
        /// </summary>
        private string? ResolvePhysicalPath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath) || _environment.WebRootPath == null)
            {
                return null;
            }

            var relative = storedPath.StartsWith("~") ? storedPath.Substring(1) : storedPath;
            relative = relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_environment.WebRootPath, relative);
        }
    }
}
