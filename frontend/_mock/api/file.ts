import { MockRequest } from '../core/models';

// 这是一个简化的 DTO，实际项目中应从 services 目录导入
export interface FileInfoRes {
  id: string;
  filename: string;
  url: string;
  creatorUsername: string;
  createdTime: Date;
}

export interface FileInfo {
  id: string;
  filename: string;
  filecontent: string;
  creatorUsername: string;
  createdTime: Date;
}

const files: FileInfo[] = [];

async function upload(params: File): Promise<FileInfoRes> {
  const fileContent = await readFileAsync(params);
  const file: FileInfo = {
    id: new Date().getTime().toString(),
    filename: params.name,
    filecontent: fileContent?.toString() ?? '',
    creatorUsername: 'zengql',
    createdTime: new Date()
  };
  files.push(file);

  return {
    id: file.id,
    filename: file.filename,
    url: `/uploads/${file.filename}`, // 返回一个模拟的 URL
    creatorUsername: file.creatorUsername,
    createdTime: file.createdTime
  };
}

function readFileAsync(file: File): Promise<string | ArrayBuffer | null> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      resolve(reader.result);
    };
    reader.onerror = () => {
      reject(reader.error);
    };
    reader.readAsDataURL(file);
  });
}

export const FILE_API = {
  'POST /api/v1/files': (req: MockRequest) => upload(req.body.get('file'))
};
