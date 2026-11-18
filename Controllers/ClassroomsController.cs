using KirkukInstitute.Data;
using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KirkukInstitute.Controllers;

[Authorize(Roles = Roles.Administrator + "," + Roles.Manager + "," + Roles.Registrar)]
public class ClassroomsController : Controller
{
    private readonly AppDbContext _db;
    private readonly Localizer _l;
    private readonly AuditService _audit;

    public ClassroomsController(AppDbContext db, Localizer l, AuditService audit)
    {
        _db = db;
        _l = l;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var rooms = await _db.Classrooms
            .Include(r => r.Schedules).ThenInclude(s => s.Section).ThenInclude(x => x.Course)
            .Include(r => r.Bookings)
            .OrderBy(r => r.Number).ToListAsync();
        ViewBag.Today = DateTime.Today.DayOfWeek;
        ViewBag.Upcoming = await _db.ClassroomBookings
            .Include(b => b.Classroom)
            .Where(b => b.Date >= DateTime.Today)
            .OrderBy(b => b.Date).ThenBy(b => b.StartTime).Take(6).ToListAsync();
        return View(rooms);
    }

    public IActionResult Create() => View("Form", new ClassroomFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClassroomFormViewModel model)
    {
        if (await _db.Classrooms.AnyAsync(r => r.Number == model.Number))
            ModelState.AddModelError(nameof(model.Number), _l.T("This room number already exists.", "رقم القاعة مستخدم مسبقاً."));
        if (!ModelState.IsValid) return View("Form", model);
        var room = new Classroom
        {
            Number = model.Number.Trim(),
            NameAr = model.NameAr,
            NameEn = model.NameEn,
            Floor = model.Floor,
            Capacity = model.Capacity,
            RoomType = model.RoomType,
            Equipment = model.Equipment,
            RentPerHour = model.RentPerHour,
            Status = model.Status,
            BranchId = 1
        };
        _db.Classrooms.Add(room);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Classroom", room.Id.ToString(), null, new { room.Number, room.Capacity, room.RentPerHour });
        TempData["Success"] = _l.T("Classroom added.", "تمت إضافة القاعة.");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var r = await _db.Classrooms.FindAsync(id);
        if (r is null) return NotFound();
        return View("Form", new ClassroomFormViewModel
        {
            Id = r.Id,
            Number = r.Number,
            NameAr = r.NameAr,
            NameEn = r.NameEn,
            Floor = r.Floor,
            Capacity = r.Capacity,
            RoomType = r.RoomType,
            Equipment = r.Equipment,
            RentPerHour = r.RentPerHour,
            Status = r.Status
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ClassroomFormViewModel model)
    {
        var room = await _db.Classrooms.FindAsync(id);
        if (room is null) return NotFound();
        if (await _db.Classrooms.AnyAsync(r => r.Number == model.Number && r.Id != id))
            ModelState.AddModelError(nameof(model.Number), _l.T("This room number already exists.", "رقم القاعة مستخدم مسبقاً."));
        if (!ModelState.IsValid) return View("Form", model);
        var old = new { room.Number, room.NameAr, room.Capacity, room.RentPerHour, room.Status };
        room.Number = model.Number.Trim();
        room.NameAr = model.NameAr;
        room.NameEn = model.NameEn;
        room.Floor = model.Floor;
        room.Capacity = model.Capacity;
        room.RoomType = model.RoomType;
        room.Equipment = model.Equipment;
        room.RentPerHour = model.RentPerHour;
        room.Status = model.Status;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Classroom", id.ToString(), old, new { room.Number, room.NameAr, room.Capacity, room.RentPerHour, room.Status });
        TempData["Success"] = _l.T("Classroom updated.", "تم تحديث القاعة.");
        return RedirectToAction(nameof(Index));
    }
}
