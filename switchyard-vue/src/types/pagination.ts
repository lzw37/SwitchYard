export interface PagedResult<T> {
    items: T[]
    pageNumber: number
    pageSize: number
    totalCount: number
    totalPages: number
}

export const DEFAULT_PAGE_SIZES = [10, 20, 50, 100] as const
