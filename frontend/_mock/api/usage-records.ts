import { UsageRecordOutputDto, UsageRecordDetailOutputDto } from '../../src/app/features/platform/models/usage.dto';
import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { MockException, MockRequest } from '../core/models';
import { getUsageRecordDetail, getUsageRecordsByUserId } from '../data/usage-record';
import { getCurrentUserId } from '../utils/current-user';

function getPagedList(req: MockRequest): PagedResultDto<UsageRecordOutputDto> {
  const {
    offset = 0,
    limit = 10,
    apiKeyName,
    model,
    accountTokenName,
    providerGroupId,
    provider,
    startTime,
    endTime,
    sorting,
    authMethod,
    downUserAgent,
    status
  } = req.queryParams;

  const currentUserId = getCurrentUserId(req);
  let filteredRecords = getUsageRecordsByUserId(currentUserId);

  if (apiKeyName && String(apiKeyName) !== 'undefined') {
    const query = String(apiKeyName).toLowerCase();
    filteredRecords = filteredRecords.filter(r => r.apiKeyName.toLowerCase().includes(query));
  }

  if (model && String(model) !== 'undefined') {
    const query = String(model).toLowerCase();
    filteredRecords = filteredRecords.filter(
      r => r.downModelId?.toLowerCase().includes(query) || r.upModelId?.toLowerCase().includes(query)
    );
  }

  if (downUserAgent && String(downUserAgent) !== 'undefined') {
    const query = String(downUserAgent).toLowerCase();
    filteredRecords = filteredRecords.filter(r => r.downUserAgent?.toLowerCase().includes(query));
  }

  if (status && String(status) !== 'undefined') {
    filteredRecords = filteredRecords.filter(r => r.status === String(status));
  }

  if (accountTokenName && String(accountTokenName) !== 'undefined') {
    const query = String(accountTokenName).toLowerCase();
    filteredRecords = filteredRecords.filter(r => r.accountTokenName?.toLowerCase().includes(query));
  }

  if (providerGroupId && String(providerGroupId) !== 'undefined') {
    filteredRecords = filteredRecords.filter(r => r.providerGroupId === String(providerGroupId));
  }

  if (provider && String(provider) !== 'undefined') {
    filteredRecords = filteredRecords.filter(r => r.provider === String(provider));
  }

  if (authMethod) {
    filteredRecords = filteredRecords.filter(r => r.authMethod === String(authMethod));
  }

  if (startTime && String(startTime) !== 'undefined') {
    const start = new Date(String(startTime)).getTime();
    if (!isNaN(start)) {
      filteredRecords = filteredRecords.filter(r => new Date(r.creationTime).getTime() >= start);
    }
  }

  if (endTime && String(endTime) !== 'undefined') {
    const end = new Date(String(endTime)).getTime();
    if (!isNaN(end)) {
      filteredRecords = filteredRecords.filter(r => new Date(r.creationTime).getTime() <= end);
    }
  }

  if (sorting && String(sorting) !== 'undefined') {
    const sortParts = String(sorting).trim().split(' ');
    const order = sortParts[sortParts.length - 1].toLowerCase();
    let field = sortParts.slice(0, sortParts.length - 1).join(' ');

    if (!field) {
      field = sortParts[0];
    }

    filteredRecords.sort((a, b) => {
      let valA: any;
      let valB: any;

      if (field === 'InputTokens + OutputTokens') {
        valA = (a.inputTokens || 0) + (a.outputTokens || 0);
        valB = (b.inputTokens || 0) + (b.outputTokens || 0);
      } else {
        valA = (a as any)[field];
        valB = (b as any)[field];
      }

      if (field === 'creationTime') {
        valA = new Date(valA).getTime();
        valB = new Date(valB).getTime();
      }

      if (valA < valB) return order === 'desc' ? 1 : -1;
      if (valA > valB) return order === 'desc' ? -1 : 1;
      return 0;
    });
  }

  const totalCount = filteredRecords.length;
  const start = +offset;
  const end = start + +limit;
  const items = filteredRecords.slice(start, end);

  return {
    totalCount,
    items
  };
}

function getDetail(req: MockRequest): UsageRecordDetailOutputDto {
  const currentUserId = getCurrentUserId(req);
  const id = req.url.split('/')[4];
  const record = getUsageRecordsByUserId(currentUserId).find(item => item.id === id);
  if (!record) {
    throw new MockException(404, { message: 'Usage record not found' });
  }

  const detail = getUsageRecordDetail(id);
  if (!detail) {
    throw new MockException(404, { message: 'Usage record detail not found' });
  }

  return detail;
}

export const USAGE_RECORDS_API = {
  'GET /api/v1/usage-records': (req: MockRequest) => getPagedList(req),
  'GET /api/v1/usage-records/:id/detail': (req: MockRequest) => getDetail(req)
};
