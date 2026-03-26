import { UsageRecordOutputDto, UsageRecordDetailOutputDto } from '../../src/app/features/platform/models/usage.dto';
import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { USAGE_RECORDS, getUsageRecordDetail } from '../data/usage-record';

function getPagedList(req: any): PagedResultDto<UsageRecordOutputDto> {
  const {
    offset = 0,
    limit = 10,
    apiKeyName,
    model,
    accountTokenName,
    providerGroupId,
    platform,
    startTime,
    endTime,
    sorting
  } = req.queryParams;

  let filteredRecords = USAGE_RECORDS;

  // Filter: API Key Name (substring, case-insensitive)
  if (apiKeyName && String(apiKeyName) !== 'undefined') {
    const query = String(apiKeyName).toLowerCase();
    filteredRecords = filteredRecords.filter(r => r.apiKeyName.toLowerCase().includes(query));
  }

  // Filter: Model (substring, case-insensitive)
  if (model && String(model) !== 'undefined') {
    const query = String(model).toLowerCase();
    filteredRecords = filteredRecords.filter(
      r => r.downModelId?.toLowerCase().includes(query)
    );
  }

  // Filter: Account Token Name (substring, case-insensitive)
  if (accountTokenName && String(accountTokenName) !== 'undefined') {
    const query = String(accountTokenName).toLowerCase();
    filteredRecords = filteredRecords.filter(r => r.accountTokenName.toLowerCase().includes(query));
  }

  // Filter: Provider Group ID (exact)
  if (providerGroupId && String(providerGroupId) !== 'undefined') {
    filteredRecords = filteredRecords.filter(r => r.providerGroupId === String(providerGroupId));
  }

  // Filter: Platform (exact)
  if (platform && String(platform) !== 'undefined') {
    filteredRecords = filteredRecords.filter(r => r.platform === String(platform));
  }

  // Filter: Time Range
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

  // Sorting
  if (sorting && String(sorting) !== 'undefined') {
    const sortParts = String(sorting).trim().split(' ');
    const order = sortParts[sortParts.length - 1].toLowerCase();
    let field = sortParts.slice(0, sortParts.length - 1).join(' ');

    if (!field) {
      // handle case where format might just be "field" (default asc) or just "asc/desc" (invalid)
      field = sortParts[0];
    }

    filteredRecords.sort((a, b) => {
      let valA: any, valB: any;

      if (field === 'InputTokens + OutputTokens') {
        valA = (a.inputTokens || 0) + (a.outputTokens || 0);
        valB = (b.inputTokens || 0) + (b.outputTokens || 0);
      } else {
        // Handle generic fields
        valA = (a as any)[field];
        valB = (b as any)[field];
      }

      // Handle dates specifically if needed, but string comparison often works for ISO
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
  // Handle pagination
  const start = +offset;
  const end = start + +limit;
  const items = filteredRecords.slice(start, end);

  return {
    totalCount: totalCount,
    items: items
  };
}

function getDetail(req: any): UsageRecordDetailOutputDto {
  const id = req.url.split('/')[4]; // /api/v1/usage-records/{id}/detail
  const detail = getUsageRecordDetail(id);

  if (!detail) throw new Error('Not Found');

  return detail;
}

export const USAGE_RECORDS_API = {
  'GET /api/v1/usage-records': (req: any) => getPagedList(req),
  'GET /api/v1/usage-records/:id/detail': (req: any) => getDetail(req)
};
