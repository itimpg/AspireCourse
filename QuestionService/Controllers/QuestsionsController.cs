using Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using QuestionService.Services;
using System.Security.Claims;
using Wolverine;

namespace QuestionService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuestionsController(QuestionDbContext db, IMessageBus bus, TagService tagService) : ControllerBase
    {
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
        {
            if (!await tagService.AreTagsValidAsync(dto.Tags))
            {
                return BadRequest($"Invalid tags");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue("name");

            if (userId is null || name is null) return BadRequest("Cannot get user details");

            var question = new Question
            {
                Title = dto.Title,
                Content = dto.Content,
                TagSlugs = dto.Tags,
                AskerId = userId,
                AskerDisplayName = name,
            };

            db.Questions.Add(question);
            await db.SaveChangesAsync();

            await bus.PublishAsync(new QuestionCreated(
                question.Id, question.Title, question.Content, question.CreatedAt, question.TagSlugs));

            return Created($"/questions/{question.Id}", question);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Question>>> GetQuestions(string? tag)
        {
            var query = db.Questions.AsEnumerable();

            if (!string.IsNullOrEmpty(tag))
            {
                query = query.Where(x => x.TagSlugs.Contains(tag));
            }

            var questions = query.OrderByDescending(x => x.CreatedAt).ToList();
            return Ok(questions);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Question>> GetQuestion(string id)
        {
            var question = await db.Questions.FindAsync(id);

            if (question is null)
            {
                return NotFound();
            }

            await db.Questions.Where(x => x.Id == id)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(
                        x => x.ViewCount, x => x.ViewCount + 1));

            return Ok(question);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<Question>> UpdateQuestion(string id, CreateQuestionDto dto)
        {
            if (!await tagService.AreTagsValidAsync(dto.Tags))
            {
                return BadRequest($"Invalid tags");
            }

            var question = await db.Questions.FindAsync(id);

            if (question is null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (question.AskerId != userId)
            {
                return Forbid();
            }

            question.Title = dto.Title;
            question.Content = dto.Content;
            question.TagSlugs = dto.Tags;
            question.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await bus.PublishAsync(new QuestionUpdated(
               question.Id, question.Title, question.Content, question.CreatedAt, question.TagSlugs));

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestion(string id)
        {
            var question = await db.Questions.FindAsync(id);

            if (question is null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (question.AskerId != userId)
            {
                return Forbid();
            }

            db.Questions.Remove(question);
            await db.SaveChangesAsync();
            await bus.PublishAsync(new QuestionDeleted(question.Id));

            return NoContent();
        }
    }
}
