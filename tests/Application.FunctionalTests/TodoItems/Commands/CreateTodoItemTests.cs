using greenfield_checkout.Application.Common.Exceptions;
using greenfield_checkout.Application.TodoItems.Commands.CreateTodoItem;
using greenfield_checkout.Application.TodoLists.Commands.CreateTodoList;
using greenfield_checkout.Domain.Entities;

namespace greenfield_checkout.Application.FunctionalTests.TodoItems.Commands;

public class CreateTodoItemTests : TestBase
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        var command = new CreateTodoItemCommand();

        await Should.ThrowAsync<ValidationException>(() => TestApp.SendAsync(command));
    }

    [Test]
    public async Task ShouldCreateTodoItem()
    {
        var userId = await TestApp.RunAsDefaultUserAsync();

        var listId = await TestApp.SendAsync(new CreateTodoListCommand
        {
            Title = "New List"
        });

        var command = new CreateTodoItemCommand
        {
            ListId = listId,
            Title = "Tasks"
        };

        var itemId = await TestApp.SendAsync(command);

        var item = await TestApp.FindAsync<TodoItem>(itemId);

        item.ShouldNotBeNull();
        item!.ListId.ShouldBe(command.ListId);
        item.Title.ShouldBe(command.Title);
        item.CreatedBy.ShouldBe(userId);
        item.Created.ShouldBe(DateTime.Now, TimeSpan.FromMilliseconds(10000));
        item.LastModifiedBy.ShouldBe(userId);
        item.LastModified.ShouldBe(DateTime.Now, TimeSpan.FromMilliseconds(10000));
    }
}
