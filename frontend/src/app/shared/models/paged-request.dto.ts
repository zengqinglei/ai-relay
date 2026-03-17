/**
 * 分页查询请求基类
 *
 * 对应后端: Leistd.Ddd.Application.Contracts.Dtos.PagedRequestDto
 *
 * 使用示例:
 * ```typescript
 * export interface GetUsersInputDto extends PagedRequestDto {
 *   keyword?: string;
 * }
 * ```
 */
export interface PagedRequestDto {
  /**
   * 偏移量（跳过的记录数）
   *
   * @default 0
   */
  offset?: number;

  /**
   * 每页记录数
   *
   * @default 10
   */
  limit?: number;

  /**
   * 排序字段（可选）
   *
   * 格式: "fieldName asc" 或 "fieldName desc"
   * 示例: "creationTime desc"
   */
  sorting?: string;
}
