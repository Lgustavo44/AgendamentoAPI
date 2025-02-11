using AgendamentoAPI.Requests;
using AgendamentoAPI.Response;
using Agendamentos.Shared.Dados.Database;
using Agendamentos.Shared.Modelos.Modelos;
using AgendamentosAPI.Shared.Dados.Database;
using AgendamentosAPI.Shared.Models.Modelos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agendamentos.EndPoints
{
    public static class AgendamentosExtensions
    {

        public static void AddEndPointsAgendamentos(this WebApplication app)
        {
            var groupBuilder = app.MapGroup("agendamentos")
                .WithTags("Agendamentos");

            groupBuilder.MapGet("", [Authorize(Roles = "Gestor")] ([FromServices] AgendamentoService agendamentoService) =>
             {
                 var listaDeAgendamentos = agendamentoService.ListarAgendamentos();

                 if (listaDeAgendamentos is null || !listaDeAgendamentos.Any())
                 {
                     return Results.Ok("Nenhum agendamento encontrado.");
                 }

                 var listaDeAgendamentosResponse = new List<AgendamentoResponse>();

                 foreach (var agendamento in listaDeAgendamentos)
                 {
                     foreach (var aulaAgendada in agendamento.AgendamentoAulas)
                     {
                         var agendamentoResponse = new AgendamentoResponse(
                             agendamento.Id,
                             agendamento.Data,
                             aulaAgendada.Aula?.Aula!,
                             agendamento.Equipamento?.Nome!,
                             agendamento.Professor?.UserName!
                         );
                         listaDeAgendamentosResponse.Add(agendamentoResponse);
                     }
                 }
                 return Results.Ok(listaDeAgendamentosResponse);

             });


            groupBuilder.MapGet("{professorId}", ([FromServices] AgendamentoService agendamentoService, string professorId) =>
            {
                var agendamentosDoProfessor = agendamentoService.
                ListarAgendamentos()
                .Where(a => a.Professor.Id == professorId);

                if (agendamentosDoProfessor is null || !agendamentosDoProfessor.Any())
                {
                    return Results.Ok($"Nenhum agendamento encontrado para o professor com ID {professorId}.");
                }

                var listaDeAgendamentosResponse = new List<AgendamentoResponse>();
                foreach (var agendamento in agendamentosDoProfessor)
                {
                    foreach (var aulaAgendada in agendamento.AgendamentoAulas)
                    {
                        var agendamentoResponse = new AgendamentoResponse(
                            agendamento.Id,
                            agendamento.Data,
                            aulaAgendada.Aula?.Aula!,
                            agendamento.Equipamento?.Nome!,
                            agendamento.Professor?.UserName!
                        );
                        listaDeAgendamentosResponse.Add(agendamentoResponse);
                    }
                }
                return Results.Ok(listaDeAgendamentosResponse);
            });

            groupBuilder.MapPost("", ([FromServices] DAL<Agendamento> dal,
                                       [FromServices] DAL<Equipamentos> EquipamentosDal,
                                       [FromBody] AgendamentoRequest agendamentoRequest,
                                       [FromServices] AgendamentosContext agendamentoContext
                                       ) =>
            {
                List<string> response = [];

                try
                {
                    if (agendamentoRequest is null) throw new Exception("O agendamento não pode ser nulo!");

                    foreach (var aula in agendamentoRequest.AulaIds)
                    {
                        var agendamentoExists = agendamentoContext.Agendamentos
                            .Where(p => p.AulaId == aula)
                            .Where(p => p.ProfessorId == agendamentoRequest.ProfessorId)
                            .Where(p => p.EquipamentoId == agendamentoRequest.EquipamentoId)
                            .Where(p => p.Data.Date == agendamentoRequest.Data.Date)
                            .Any();

                        if (agendamentoExists)
                        {
                            return Results.BadRequest(new { Message = $"Este agendamento na {aula}º aula já existe!" });
                        }
                        else
                        {
                            var equipamento = agendamentoContext
                              .Equipamentos.FirstOrDefault(p => p.Id == agendamentoRequest.EquipamentoId)!.Quantidade;

                            var agendamentosCount = agendamentoContext.Agendamentos
                            .Where(p => p.AulaId == aula)
                            .Where(p => p.EquipamentoId == agendamentoRequest.EquipamentoId)
                            .Where(p => p.Data.Date == agendamentoRequest.Data.Date)
                            .Count();

                            if (agendamentosCount >= equipamento)
                            {
                                return Results.BadRequest(new { Message = $"Equipamento insuficiente para atender a {aula}º aula!" });
                            }
                            else
                            {
                                Agendamento novoAgendamento = new Agendamento
                                {
                                    ProfessorId = agendamentoRequest.ProfessorId,
                                    EquipamentoId = agendamentoRequest.EquipamentoId,
                                    AulaId = aula,
                                    Data = agendamentoRequest.Data,
                                };

                                novoAgendamento.AgendamentoAulas.Add(new AgendamentoAula { AulaId = aula });

                                dal.Adicionar(novoAgendamento);

                                response.Add("Agendamento realizado com sucesso!");
                            }
                        }
                    }

                    return Results.Ok(response);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { Message = ex.Message });
                }
            });

            groupBuilder.MapDelete("{id}", (
                [FromServices] DAL<Agendamento> dal,
                int id) =>
            {
                var agendamento = dal.RecuperarPor(a => a.Id == id);
                if (agendamento is null)
                {
                    return Results.NotFound();
                }
                dal.Deletar(agendamento);

                return Results.NoContent();
            });

            groupBuilder.MapPut("", (
                [FromServices] DAL<Agendamento> dal,
                [FromBody] AgendamentosRequestEdit agendamentoRequestEdit) =>
            {
                var agendamentoAAtualizar = dal.RecuperarPor(a => a.Id == agendamentoRequestEdit.Id);
                if (agendamentoAAtualizar is null)
                {
                    return Results.NotFound();
                }
                agendamentoAAtualizar.Data = agendamentoRequestEdit.Data;
                agendamentoAAtualizar.EquipamentoId = agendamentoRequestEdit.EquipamentoId;
                agendamentoAAtualizar.ProfessorId = agendamentoRequestEdit.ProfessorId;

                agendamentoAAtualizar.AgendamentoAulas.Clear();
                foreach (var aulaId in agendamentoRequestEdit.AulaIds)
                {
                    agendamentoAAtualizar.AgendamentoAulas.Add(new AgendamentoAula { AulaId = aulaId });
                }
                dal.Atualizar(agendamentoAAtualizar);

                return Results.Ok();
            });
        }
    }
}
