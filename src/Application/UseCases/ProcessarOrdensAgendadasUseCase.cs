using CaseGig.Application.Abstractions;
using CaseGig.Application.Exceptions;
using CaseGig.Domain.Enums;
using CaseGig.Domain.Exceptions;
using CaseGig.Domain.Services;
using Microsoft.Extensions.Logging;

namespace CaseGig.Application.UseCases;

public sealed class ProcessarOrdensAgendadasUseCase
{
    private readonly ILogger<ProcessarOrdensAgendadasUseCase> _logger;
    private readonly ITransactionManager _transactionManager;
    private readonly IClienteRepository _clienteRepository;
    private readonly IFundoRepository _fundoRepository;
    private readonly IPosicaoRepository _posicaoRepository;
    private readonly IOrdemRepository _ordemRepository;
    private readonly OrdemProcessamentoService _processamentoService;

    public ProcessarOrdensAgendadasUseCase(
        ILogger<ProcessarOrdensAgendadasUseCase> logger,
        ITransactionManager transactionManager,
        IClienteRepository clienteRepository,
        IFundoRepository fundoRepository,
        IPosicaoRepository posicaoRepository,
        IOrdemRepository ordemRepository,
        OrdemProcessamentoService processamentoService)
    {
        _logger = logger;
        _transactionManager = transactionManager;
        _clienteRepository = clienteRepository;
        _fundoRepository = fundoRepository;
        _posicaoRepository = posicaoRepository;
        _ordemRepository = ordemRepository;
        _processamentoService = processamentoService;
    }

    public async Task<ProcessamentoResumo> ExecuteAsync(DateTime agora, int maximo, CancellationToken cancellationToken)
    {
        var ordens = await _ordemRepository.ListAgendadasParaProcessarAsync(agora, maximo, cancellationToken);
        var encontradas = ordens.Count;

        var processadas = 0;
        var rejeitadas = 0;
        var conflitosConcorrencia = 0;
        var erros = 0;

        foreach (var ordem in ordens)
        {
            try
            {
                _logger.LogInformation(
                    "WORKER: Processando ordem agendada. Ordem={IdOrdem} Cliente={IdCliente} Fundo={IdFundo} Tipo={TipoOperacao}",
                    ordem.IdOrdem,
                    ordem.IdCliente,
                    ordem.IdFundo,
                    ordem.TipoOperacao);

                await _transactionManager.ExecuteAsync(async ct =>
                {
                    var cliente = await _clienteRepository.GetByIdAsync(ordem.IdCliente, ct);
                    if (cliente is null)
                    {
                        throw new BusinessRuleException("Cliente não encontrado.");
                    }

                    var fundo = await _fundoRepository.GetByIdAsync(ordem.IdFundo, ct);
                    if (fundo is null)
                    {
                        throw new BusinessRuleException("Fundo não encontrado.");
                    }

                    var posicao = await _posicaoRepository.GetByIdAsync(ordem.IdCliente, ordem.IdFundo, ct);

                    try
                    {
                        _processamentoService.PrepararParaProcessamento(ordem, agora);

                        if (ordem.TipoOperacao == TipoOperacao.APORTE)
                        {
                            _processamentoService.ProcessarOrdemAporte(ordem, cliente, fundo, posicao);
                        }
                        else
                        {
                            if (posicao is null)
                            {
                                throw new BusinessRuleException("Cotas insuficientes para realizar o resgate.");
                            }

                            _processamentoService.ProcessarOrdemResgate(ordem, cliente, fundo, posicao);
                        }

                        _processamentoService.Concluir(ordem, agora);
                        processadas++;

                        _logger.LogInformation(
                            "WORKER: Ordem processada com sucesso. Ordem={IdOrdem} Status={Status}",
                            ordem.IdOrdem,
                            ordem.Status);
                    }
                    catch (BusinessRuleException ex)
                    {
                        _processamentoService.Rejeitar(ordem, agora);
                        rejeitadas++;

                        _logger.LogWarning(
                            "WORKER: Ordem rejeitada no processamento. Ordem={IdOrdem} Motivo={Motivo}",
                            ordem.IdOrdem,
                            ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WORKER: Erro inesperado durante o processamento da ordem. Ordem={IdOrdem}", ordem.IdOrdem);
                        throw;
                    }
                }, cancellationToken);
            }
            catch (ConcurrencyException)
            {
                conflitosConcorrencia++;
                _logger.LogWarning("WORKER: Conflito de concorrência ao processar ordem. Ordem={IdOrdem}", ordem.IdOrdem);
            }
            catch (Exception ex)
            {
                erros++;
                _logger.LogError(ex, "WORKER: Falha ao processar ordem agendada. Ordem={IdOrdem}", ordem.IdOrdem);
            }
        }

        return new ProcessamentoResumo(encontradas, processadas, rejeitadas, conflitosConcorrencia, erros);
    }
}

public sealed record ProcessamentoResumo(int Encontradas, int Processadas, int Rejeitadas, int ConflitosConcorrencia, int Erros);
