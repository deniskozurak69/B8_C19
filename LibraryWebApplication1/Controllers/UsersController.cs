using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryWebApplication1.Models;
using Microsoft.AspNetCore.Authorization;
using LibraryWebApplication1.Services;
using Microsoft.Extensions.Caching.Memory;
namespace LibraryWebApplication1.Controllers
{
    public class UsersController : Controller
    {
        private readonly DblibraryContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly FileProcessingService _fileProcessingService;
        private readonly IMemoryCache _memoryCache;
        private readonly AzureBlobService _blobService;
        public UsersController(DblibraryContext context, IWebHostEnvironment webHostEnvironment,FileProcessingService fileProcessingService, IMemoryCache memoryCache)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _fileProcessingService = fileProcessingService;
            _memoryCache = memoryCache;
            string connectionString = "DefaultEndpointsProtocol=https;AccountName=denisstorageaccount;AccountKey=8Z/0rX0DcP6Os2ZgyuEI0d9PUfDeOWY+WP+WmxVB1dP/Mnj0Z5HcXJzDeu8OrXshmdFESV5WSEdo+ASt1ZnsbQ==;EndpointSuffix=core.windows.net";
            string containerName = "deniscontainers";
            _blobService = new AzureBlobService(connectionString, containerName, webHostEnvironment);

        }
        private async Task<List<User>> GetCachedUsersWithArticlesAsync()
        {
            string cacheKey = "users_with_articles";
            if (!_memoryCache.TryGetValue(cacheKey, out List<User> users))
            {
                users = await _context.Users
                    .Include(u => u.Articles) 
                    .ThenInclude(a => a.CategoryNavigation)
                    .ToListAsync();
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetSlidingExpiration(TimeSpan.FromMinutes(10));
                _memoryCache.Set(cacheKey, users, cacheOptions);
            }
            return users;
        }
        public async Task<IActionResult> Index()
        {
            var users = await GetCachedUsersWithArticlesAsync();
            return View(users);
        }
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var users = await GetCachedUsersWithArticlesAsync();
            var user = users.FirstOrDefault(u => u.UserId == id);
            if (user == null) return NotFound();
            return View(user);
        }
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,Username,Password,Latitude,Longtitude")] User user, IFormFile photoFile, bool compressImage)
        {
            string defaultProfilePhotoUrl = "https://denisstorageaccount.blob.core.windows.net/deniscontainers/5393eb4f-4537-4478-bdd6-7dfd3401d8ba.jpg";
            if (photoFile != null && photoFile.Length > 0)
            {
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(photoFile.FileName);
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                string filePath = Path.Combine(uploadsFolder, fileName);                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await photoFile.CopyToAsync(stream);
                }
                user.ProfilePhoto = Path.Combine("/uploads", fileName);
                await _fileProcessingService.SendFileProcessingMessageAsync(filePath);
                string newFileName = Path.Combine(Path.GetFileNameWithoutExtension(fileName) + "_compressed.jpg");
                if (compressImage)
                {
                    user.ProfilePhoto = Path.Combine("/uploads", newFileName);                    
                }
            }
            else
            {
                //ModelState.AddModelError("ProfilePhotoFile", "Файл не додано");
                user.ProfilePhoto = defaultProfilePhotoUrl;
                //return View(user);
            }
            int maxUserId = _context.Users.Max(c => (int?)c.UserId) ?? 0;
            user.UserId = maxUserId + 1;
            _context.Add(user);
            await _context.SaveChangesAsync();
            _memoryCache.Remove("articles");
            _memoryCache.Remove("categories_with_articles");
            _memoryCache.Remove("users_with_articles");
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("UserId,Username,Password,Latitude,Longtitude")] User user, IFormFile photoFile, bool compressImage)
        {
            if (id != user.UserId)
            {
                return NotFound();
            }
            try
            {
                if (photoFile != null && photoFile.Length > 0)
                {
                    var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(photoFile.FileName);
                    var filePath = Path.Combine(uploadsPath, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await photoFile.CopyToAsync(stream);
                    }
                    user.ProfilePhoto = Path.Combine("uploads", fileName);
                    await _fileProcessingService.SendFileProcessingMessageAsync(filePath);
                    string newFileName = Path.Combine(Path.GetFileNameWithoutExtension(fileName) + "_compressed.jpg");
                    if (compressImage)
                    {
                        user.ProfilePhoto = Path.Combine("/uploads", newFileName);
                    }
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                    _memoryCache.Remove("articles");
                    _memoryCache.Remove("categories_with_articles");
                    _memoryCache.Remove("users_with_articles");
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(user.UserId)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
            return View(user);
        }
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                var articlesToDelete = _context.Articles.Where(a => a.AuthorId == user.UserId);
                _context.Articles.RemoveRange(articlesToDelete);
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                _memoryCache.Remove("articles");
                _memoryCache.Remove("categories_with_articles");
                _memoryCache.Remove("users_with_articles");
            }
            return RedirectToAction(nameof(Index));
        }
        public IActionResult GetUsersLocations()
        {
            var users = _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Latitude,
                    u.Longtitude
                })
                .ToList();
            return Json(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetAutocompleteData(string term)
        {
            var users = await _context.Users
                .Where(u => u.Username.Contains(term))
                .Select(u => new { u.UserId, u.Username })
                .ToListAsync();
            foreach (var user in users)
            {
                Console.WriteLine($"Username: {user.Username}");
            }
            return Json(users);
        }

        [HttpPost]
        public async Task<IActionResult> UploadToAzure(int userId, string filePath)
        {
            try
            {
                string blobUrl = await _blobService.UploadFileToBlobAsync(filePath);
                return Ok(new { message = "File uploaded to Azure successfully!", url = blobUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error uploading file", error = ex.Message });
            }
        }
        public IActionResult UsersMap()
        {
            ViewBag.MapboxAccessToken = "pk.eyJ1IjoiZGVuaXNrb3p1cmFrIiwiYSI6ImNtMThlMXpzZTB4cnYybHNjMWp1b2F2cWkifQ.NkJPtAFCyuIrU_dys4KN6Q";
            return View();
        }
        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }
    }
}

