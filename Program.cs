using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IBookRepository, InMemoryBookRepository>();

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public interface IBookRepository
{
    IEnumerable<Book> GetAll();
    Book? GetById(int id);
    void Add(Book book);
    void Update(Book book);
    void Delete(int id);
}

public class InMemoryBookRepository : IBookRepository
{
    private readonly List<Book> _books = new();
    private int _nextId = 1;

    public IEnumerable<Book> GetAll() => _books;

    public Book? GetById(int id) => _books.FirstOrDefault(b => b.Id == id);

    public void Add(Book book)
    {
        book.Id = _nextId++;
        _books.Add(book);
    }

    public void Update(Book book)
    {
        var existing = GetById(book.Id);
        if (existing != null)
        {
            existing.Title = book.Title;
            existing.Author = book.Author;
            existing.Year = book.Year;
            existing.IsAvailable = book.IsAvailable;
        }
    }

    public void Delete(int id)
    {
        var book = GetById(id);
        if (book != null)
        {
            _books.Remove(book);
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IBookRepository _repository;

    public BooksController(IBookRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public ActionResult<IEnumerable<Book>> GetAll()
    {
        return Ok(_repository.GetAll());
    }

    [HttpGet("{id}")]
    public ActionResult<Book> GetById(int id)
    {
        var book = _repository.GetById(id);
        if (book == null) return NotFound();
        return Ok(book);
    }

    [HttpPost]
    public ActionResult<Book> Add([FromBody] Book book)
    {
        if (string.IsNullOrWhiteSpace(book.Title))
            return BadRequest("Title is required.");

        _repository.Add(book);
        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] Book book)
    {
        var existing = _repository.GetById(id);
        if (existing == null) return NotFound();

        book.Id = id;
        _repository.Update(book);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var existing = _repository.GetById(id);
        if (existing == null) return NotFound();

        _repository.Delete(id);
        return NoContent();
    }
}


using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

public class BooksControllerTests
{
    private readonly Mock<IBookRepository> _mockRepo;
    private readonly BooksController _controller;

    public BooksControllerTests()
    {
        _mockRepo = new Mock<IBookRepository>();
        _controller = new BooksController(_mockRepo.Object);
    }

    [Fact]
    public void GetAll_ReturnsAllBooks()
    {
        var books = new List<Book>
        {
            new Book { Id = 1, Title = "Кобзар", Author = "Тарас Шевченко", Year = 1840 },
            new Book { Id = 2, Title = "Захар Беркут", Author = "Іван Франко", Year = 1883 }
        };
        _mockRepo.Setup(r => r.GetAll()).Returns(books);

        var result = _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedBooks = Assert.IsAssignableFrom<IEnumerable<Book>>(okResult.Value);
        Assert.Equal(2, returnedBooks.Count());
    }

    [Fact]
    public void GetAll_ReturnsEmptyList_WhenNoBooksExist()
    {
        _mockRepo.Setup(r => r.GetAll()).Returns(new List<Book>());

        var result = _controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedBooks = Assert.IsAssignableFrom<IEnumerable<Book>>(okResult.Value);
        Assert.Empty(returnedBooks);
    }

    [Fact]
    public void GetById_ReturnsBook_WhenIdExists()
    {
        var book = new Book { Id = 1, Title = "Тіні забутих предків", Author = "Михайло Коцюбинський", Year = 1911 };
        _mockRepo.Setup(r => r.GetById(1)).Returns(book);

        var result = _controller.GetById(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedBook = Assert.IsType<Book>(okResult.Value);
        Assert.Equal(1, returnedBook.Id);
        Assert.Equal("Тіні забутих предків", returnedBook.Title);
    }

    [Fact]
    public void GetById_ReturnsNotFound_WhenIdDoesNotExist()
    {
        _mockRepo.Setup(r => r.GetById(99)).Returns((Book?)null);

        var result = _controller.GetById(99);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Add_AddsBookSuccessfully_WhenValid()
    {
        var newBook = new Book { Id = 1, Title = "Лісова пісня", Author = "Леся Українка", Year = 1911 };

        var result = _controller.Add(newBook);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedBook = Assert.IsType<Book>(createdResult.Value);
        Assert.Equal("Лісова пісня", returnedBook.Title);
        _mockRepo.Verify(r => r.Add(newBook), Times.Once);
    }

    [Fact]
    public void Add_ReturnsBadRequest_WhenTitleIsEmpty()
    {
        var invalidBook = new Book { Title = "", Author = "Невідомий", Year = 2020 };

        var result = _controller.Add(invalidBook);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockRepo.Verify(r => r.Add(It.IsAny<Book>()), Times.Never);
    }

    [Fact]
    public void Update_UpdatesBookSuccessfully_WhenIdExists()
    {
        var existingBook = new Book { Id = 1, Title = "Стара назва", Author = "Автор", Year = 2000 };
        var updatedBook = new Book { Id = 1, Title = "Нова назва", Author = "Автор", Year = 2021 };
        _mockRepo.Setup(r => r.GetById(1)).Returns(existingBook);

        var result = _controller.Update(1, updatedBook);

        Assert.IsType<NoContentResult>(result);
        _mockRepo.Verify(r => r.Update(updatedBook), Times.Once);
    }

    [Fact]
    public void Update_ReturnsNotFound_WhenBookDoesNotExist()
    {
        var updatedBook = new Book { Id = 99, Title = "Нова назва", Author = "Автор", Year = 2021 };
        _mockRepo.Setup(r => r.GetById(99)).Returns((Book?)null);

        var result = _controller.Update(99, updatedBook);

        Assert.IsType<NotFoundResult>(result);
        _mockRepo.Verify(r => r.Update(It.IsAny<Book>()), Times.Never);
    }

    [Fact]
    public void Delete_DeletesBookSuccessfully_WhenIdExists()
    {
        var existingBook = new Book { Id = 1, Title = "Книга для видалення" };
        _mockRepo.Setup(r => r.GetById(1)).Returns(existingBook);

        var result = _controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
        _mockRepo.Verify(r => r.Delete(1), Times.Once);
    }

    [Fact]
    public void Delete_ReturnsNotFound_WhenBookDoesNotExist()
    {
        _mockRepo.Setup(r => r.GetById(99)).Returns((Book?)null);

        var result = _controller.Delete(99);

        Assert.IsType<NotFoundResult>(result);
        _mockRepo.Verify(r => r.Delete(It.IsAny<int>()), Times.Never);
    }
}
